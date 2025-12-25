using Backend.Api;
using Backend.Api.Configuration;
using Backend.Application.Common;
using Backend.Common;
using Backend.Common.Models;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Backend.Common.Extensions;
using Backend.Features.Auth._Shared;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Features.Auth;

public sealed class RefreshToken
{
    public record RefreshTokenRequest(string Token, string RefreshToken);

    internal sealed class Handler(
        UserManager<KrafterUser> userManager,
        ITokenService tokenService,
        KrafterContext krafterContext,
        IOptions<JwtSettings> jwtSettings) : IScopedHandler
    {
        private readonly JwtSettings _jwtSettings = jwtSettings.Value;

        public async Task<Response<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress)
        {
            ClaimsPrincipal userPrincipal = GetPrincipalFromExpiredToken(request.Token);
            string? userEmail = userPrincipal.GetEmail();
            KrafterUser? user = await userManager.FindByEmailAsync(userEmail!);
            if (user is null)
            {
                throw new UnauthorizedException("Authentication Failed.");
            }

            UserRefreshToken? refreshToken =
                await krafterContext.UserRefreshTokens.FirstOrDefaultAsync(x => x.UserId == user.Id);
            if (refreshToken is null || refreshToken.RefreshToken != request.RefreshToken ||
                refreshToken.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                throw new UnauthorizedException("Invalid Refresh Token.");
            }

            return new Response<TokenResponse>
            {
                Data = await tokenService.GenerateTokensAndUpdateUser(user, ipAddress)
            };
        }

        private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key)),
                ValidateIssuer = false,
                ValidateAudience = false,
                RoleClaimType = ClaimTypes.Role,
                ClockSkew = TimeSpan.Zero,
                ValidateLifetime = false
            };
            var tokenHandler = new JwtSecurityTokenHandler();
            ClaimsPrincipal? principal =
                tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken? securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(
                    SecurityAlgorithms.HmacSha256,
                    StringComparison.InvariantCultureIgnoreCase))
            {
                throw new UnauthorizedException("Invalid Token.");
            }

            return principal;
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder productGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Tokens)
                .AddFluentValidationFilter();


            productGroup.MapPost("/refresh", async
            ([FromBody] RefreshTokenRequest request, HttpContext context,
                [FromServices] Handler handler) =>
            {
                string? ipAddress = GetIpAddress(context);
                Response<TokenResponse> res = await handler.RefreshTokenAsync(request, ipAddress!);
                return TypedResults.Ok(res);
            }).Produces<Response<TokenResponse>>();
        }

        private string? GetIpAddress(HttpContext httpContext)
        {
            return httpContext.Request.Headers.ContainsKey("X-Forwarded-For")
                ? httpContext.Request.Headers["X-Forwarded-For"]
                : httpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "N/A";
        }
    }
}
