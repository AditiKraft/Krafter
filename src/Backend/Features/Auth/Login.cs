using Backend.Api;
using Backend.Api.Configuration;
using Backend.Features.Auth._Shared;
using Backend.Features.Users._Shared;
using Krafter.Shared.Common;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Backend.Features.Auth;

public sealed class GetToken
{
    internal sealed class Handler(
        UserManager<KrafterUser> userManager,
        ITokenService tokenService,
        IOptions<JwtSettings> jwtSettings,
        IOptions<SecuritySettings> securitySettings
    ) : IScopedHandler
    {
        private readonly SecuritySettings _securitySettings = securitySettings.Value;
        private readonly JwtSettings _jwtSettings = jwtSettings.Value;

        public async Task<Response<TokenResponse>> GetTokenAsync(
            TokenRequest request, string ipAddress,
            CancellationToken cancellationToken)
        {
            KrafterUser? user = await userManager.FindByEmailAsync(request.Email.Trim().Normalize());
            if (user is null)
            {
                return Response<TokenResponse>.Unauthorized("Invalid Email or Password");
            }

            if (!await userManager.CheckPasswordAsync(user, request.Password))
            {
                return Response<TokenResponse>.BadRequest("Invalid Email or Password");
            }

            if (!user.IsActive)
            {
                return Response<TokenResponse>.BadRequest("User Not Active. Please contact the administrator.");
            }

            if (_securitySettings.RequireConfirmedAccount && !user.EmailConfirmed)
            {
                return Response<TokenResponse>.BadRequest("E-Mail not confirmed.");
            }

            return Response<TokenResponse>.Success(await tokenService.GenerateTokensAndUpdateUser(user, ipAddress));
        }
    }

    public sealed class TokenRoute : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder productGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Tokens)
                .AddFluentValidationFilter();

            productGroup.MapPost("/create", async
            ([FromBody] TokenRequest request, HttpContext context,
                [FromServices] Handler handler) =>
            {
                string? ipAddress = GetIpAddress(context);
                Response<TokenResponse> res = await handler.GetTokenAsync(request, ipAddress!, CancellationToken.None);
                return Results.Json(res, statusCode: res.StatusCode);
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
