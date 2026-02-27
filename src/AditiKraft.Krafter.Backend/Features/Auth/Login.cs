using AditiKraft.Krafter.Backend.Api;
using AditiKraft.Krafter.Backend.Api.Configuration;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AditiKraft.Krafter.Backend.Features.Auth;

public sealed class Login
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

        public async Task<Response<TokenResponse>> LoginAsync(
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

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder productGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Tokens)
                .AddFluentValidationFilter();

            productGroup.MapPost("/", async
            ([FromBody] TokenRequest request, HttpContext context,
                [FromServices] Handler handler) =>
            {
                string? ipAddress = GetIpAddress(context);
                Response<TokenResponse> res = await handler.LoginAsync(request, ipAddress!, CancellationToken.None);
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


