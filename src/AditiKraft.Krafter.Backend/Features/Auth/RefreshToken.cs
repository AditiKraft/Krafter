using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Web.Configuration;
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
        UserManager<KrafterUser> userManager,
        KrafterContext krafterContext,
        ITokenService tokenService,
        IOptions<JwtSettings> jwtSettings
    ) : IScopedHandler
    {
        private readonly JwtSettings _jwtSettings = jwtSettings.Value;

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

            KrafterUser? user = await userManager.FindByEmailAsync(userEmail);
            if (user is null)
            {
                return Response<TokenResponse>.Unauthorized("Authentication failed.");
            }

            UserRefreshToken? refreshToken = await krafterContext.UserRefreshTokens
                .FirstOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);

            if (refreshToken is null ||
                refreshToken.RefreshToken != request.RefreshToken ||
                refreshToken.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return Response<TokenResponse>.Unauthorized("Invalid or expired refresh token.");
            }

            return Response<TokenResponse>.Success(await tokenService.GenerateTokensAndUpdateUser(user, ipAddress));
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
                .MapGroup(KrafterRoute.Tokens)
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



