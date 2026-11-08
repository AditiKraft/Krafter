using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Common;
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
        IOptions<JwtSettings> jwtSettings,
        SessionValidator sessions,
        ITenantGetterService tenant
    ) : IScopedHandler
    {
        private readonly JwtSettings _jwtSettings = jwtSettings.Value;

        public async Task<Response<TokenResponse>> RefreshTokenAsync(
            RefreshTokenRequest request,
            string ipAddress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string token = request.Token;
            string refreshProof = request.RefreshToken;
            ClaimsPrincipal? principal = GetPrincipalFromExpiredToken(token);
            string? userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            string? tenantId = tenant.Tenant.Id;
            if (string.IsNullOrEmpty(refreshProof) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId) ||
                principal!.FindFirstValue(AppClaimTypes.TenantId) != tenantId)
            {
                return Response<TokenResponse>.Unauthorized("Invalid token.");
            }

            return await AccountAuthenticationTransaction.RunAsync(applicationDbContext, tenantId, userId, async () =>
            {
                // Revalidate the original credential against state reloaded under the same lock
                // as MFA changes and login. A stale refresh must never inherit a newer version.
                ClaimsPrincipal? userPrincipal = GetPrincipalFromExpiredToken(token);
                if (userPrincipal is null || !await sessions.ValidateAsync(userPrincipal, cancellationToken))
                {
                    return Response<TokenResponse>.Unauthorized("Invalid token.");
                }

                ApplicationUser? user = await userManager.FindByIdAsync(userId);
                cancellationToken.ThrowIfCancellationRequested();
                if (user is null)
                {
                    return Response<TokenResponse>.Unauthorized("Authentication failed.");
                }

                UserRefreshToken? refreshToken = await applicationDbContext.UserRefreshTokens
                    .FirstOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);

                if (refreshToken is null ||
                    refreshToken.RefreshToken != refreshProof ||
                    refreshToken.RefreshTokenExpiryTime <= DateTime.UtcNow)
                {
                    return Response<TokenResponse>.Unauthorized("Invalid or expired refresh token.");
                }

                return Response<TokenResponse>.Success(await tokenService.GenerateTokensAndUpdateUser(user, ipAddress,
                    long.Parse(userPrincipal.FindFirstValue(AppClaimTypes.AuthenticationTime)!, System.Globalization.CultureInfo.InvariantCulture),
                    userPrincipal.FindFirstValue(AppClaimTypes.AuthenticationMethod) ?? "pwd",
                    mfaVerified: userPrincipal.FindFirstValue(AppClaimTypes.Mfa) == "true", cancellationToken: cancellationToken));
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

            var tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            ClaimsPrincipal principal;
            SecurityToken securityToken;
            try
            {
                principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out securityToken);
            }
            catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
            {
                return null;
            }

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
            return httpContext.Connection.RemoteIpAddress?.ToString() ?? "N/A";
        }
    }
}



