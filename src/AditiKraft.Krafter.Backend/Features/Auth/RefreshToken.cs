using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Extensions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AditiKraft.Krafter.Backend.Features.Auth;

public sealed class RefreshToken
{
    internal sealed class Handler(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext applicationDbContext,
        ITokenService tokenService,
        IOptions<JwtSettings> jwtSettings
    ) : IScopedHandler
    {
        private readonly JwtSettings _jwtSettings = jwtSettings.Value;

        /// <summary>
        /// Window during which a just-rotated (previous) refresh token is still accepted, to absorb
        /// concurrent refreshes that raced against the rotation.
        /// </summary>
        private const int RefreshGraceSeconds = 30;

        public async Task<Response<TokenResponse>> RefreshTokenAsync(
            RefreshTokenRequest request,
            string ipAddress,
            CancellationToken cancellationToken)
        {
            ClaimsPrincipal? userPrincipal = GetPrincipalFromExpiredToken(request.Token);
            if (userPrincipal is null)
            {
                return Response<TokenResponse>.Unauthorized("Invalid token.");
            }

            string? userEmail = userPrincipal.GetEmail();

            if (string.IsNullOrEmpty(userEmail))
            {
                return Response<TokenResponse>.Unauthorized("Invalid token.");
            }

            ApplicationUser? user = await userManager.FindByEmailAsync(userEmail);
            if (user is null)
            {
                return Response<TokenResponse>.Unauthorized("Authentication failed.");
            }

            // Serialise rotation per user so concurrent refreshes are handled one at a time; the
            // later caller then re-reads the row and is served from the grace window below rather
            // than racing to rotate and clobbering the winner's token.
            return await RefreshTokenLock.RunAsync(user.Id, async () =>
            {
                UserRefreshToken? refreshToken = await applicationDbContext.UserRefreshTokens
                    .FirstOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);

                if (refreshToken is null || refreshToken.RefreshTokenExpiryTime <= DateTime.UtcNow)
                {
                    return Response<TokenResponse>.Unauthorized("Invalid or expired refresh token.");
                }

                // Normal case: the caller presents the current refresh token — rotate it.
                if (refreshToken.RefreshToken == request.RefreshToken)
                {
                    return Response<TokenResponse>.Success(
                        await tokenService.GenerateTokensAndUpdateUser(user, ipAddress));
                }

                // Grace case: a concurrent refresh already rotated this token a moment ago. The
                // caller still holds the previous value, so re-issue an access token against the
                // current refresh token instead of forcing a logout.
                bool presentsRecentlyRotated =
                    !string.IsNullOrEmpty(refreshToken.PreviousRefreshToken) &&
                    refreshToken.PreviousRefreshToken == request.RefreshToken &&
                    refreshToken.RefreshTokenRotatedAt is { } rotatedAt &&
                    rotatedAt.AddSeconds(RefreshGraceSeconds) > DateTime.UtcNow;

                if (presentsRecentlyRotated)
                {
                    return Response<TokenResponse>.Success(
                        await tokenService.GenerateAccessTokenWithoutRotation(user, ipAddress, refreshToken));
                }

                return Response<TokenResponse>.Unauthorized("Invalid or expired refresh token.");
            }, cancellationToken);
        }

        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ClockSkew = TimeSpan.Zero
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            ClaimsPrincipal principal =
                tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256,
                    StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder tokenGroup = endpointRouteBuilder
                .MapGroup(ApiRoutes.Tokens)
                .AddFluentValidationFilter();

            tokenGroup.MapPost($"/{RouteSegment.Refresh}", async (
                    [FromBody] RefreshTokenRequest request,
                    HttpContext context,
                    [FromServices] Handler handler,
                    CancellationToken cancellationToken) =>
                {
                    string? ipAddress = GetIpAddress(context);
                    Response<TokenResponse> res =
                        await handler.RefreshTokenAsync(request, ipAddress!, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<TokenResponse>>()
                .AllowAnonymous();
        }

        private static string? GetIpAddress(HttpContext httpContext)
        {
            return httpContext.Request.Headers.ContainsKey("X-Forwarded-For")
                ? httpContext.Request.Headers["X-Forwarded-For"]
                : httpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "N/A";
        }
    }
}



