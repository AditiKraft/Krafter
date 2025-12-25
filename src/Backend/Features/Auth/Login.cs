using Backend.Api;
using Backend.Api.Configuration;
using Backend.Application.Common;
using Backend.Common;
using Backend.Common.Interfaces;
using Backend.Common.Models;
using Backend.Features.Auth._Shared;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Features.Auth.Token;

public sealed class GetToken
{
    public sealed class TokenRequestInput
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool IsExternalLogin { get; set; } = false;
        public string Code { get; set; }
    }

    internal sealed class Handler(
        UserManager<KrafterUser> userManager,
        ITokenService tokenService,
        KrafterContext krafterContext,
        IOptions<JwtSettings> jwtSettings,
        IOptions<SecuritySettings> securitySettings
    ) : IScopedHandler
    {
        private readonly SecuritySettings _securitySettings = securitySettings.Value;
        private readonly JwtSettings _jwtSettings = jwtSettings.Value;

        public async Task<Response<TokenResponse>> GetTokenAsync(TokenRequestInput requestInput, string ipAddress,
            CancellationToken cancellationToken)
        {
            KrafterUser? user = await userManager.FindByEmailAsync(requestInput.Email.Trim().Normalize());
            if (user is null)
            {
                throw new UnauthorizedException("Invalid Email or Password");
            }

            if (!await userManager.CheckPasswordAsync(user, requestInput.Password))
            {
                throw new KrafterException("Invalid Email or Password");
            }

            if (!user.IsActive)
            {
                throw new KrafterException("User Not Active. Please contact the administrator.");
            }

            if (_securitySettings.RequireConfirmedAccount && !user.EmailConfirmed)
            {
                throw new KrafterException("E-Mail not confirmed.");
            }

            return new Response<TokenResponse>
            {
                Data = await tokenService.GenerateTokensAndUpdateUser(user, ipAddress)
            };
        }
    }

    public sealed class TokenRoute : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder productGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Tokens)
                .AddFluentValidationFilter();

            productGroup.MapPost("/create", async
            ([FromBody] TokenRequestInput request, HttpContext context,
                [FromServices] Handler handler) =>
            {
                string? ipAddress = GetIpAddress(context);
                Response<TokenResponse> res = await handler.GetTokenAsync(request, ipAddress!, CancellationToken.None);
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


    public class TokenRequestValidator : AbstractValidator<TokenRequestInput>
    {
        public TokenRequestValidator()
        {
            RuleFor(p => p.Email).Cascade(CascadeMode.Stop)
                .NotEmpty()
                .EmailAddress()
                .WithMessage("Invalid Email Address.");

            RuleFor(p => p.Password).Cascade(CascadeMode.Stop)
                .NotEmpty();
        }
    }
}
