using System.Security.Claims;
using System.Text;
using AditiKraft.Krafter.Backend.Errors;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Realtime;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;

namespace AditiKraft.Krafter.Backend.Web.Authentication;

public class ConfigureJwtBearerOptions(IOptions<JwtSettings> jwtSettings) : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtSettings _jwtSettings = jwtSettings.Value;

    public void Configure(JwtBearerOptions options) => Configure(string.Empty, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        byte[] key = Encoding.UTF8.GetBytes(_jwtSettings.Key);

        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateLifetime = true,
            ValidateAudience = false,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (before, expires, token, parameters) => expires > DateTime.UtcNow &&
                                                                        expires <= DateTime.UtcNow.AddMinutes(
                                                                            _jwtSettings.TokenExpirationInMinutes)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                SessionValidator validator = context.HttpContext.RequestServices.GetRequiredService<SessionValidator>();
                if (context.Principal is null || !await validator.ValidateAsync(context.Principal, context.HttpContext.RequestAborted))
                    context.Fail("Session expired. Sign in again.");
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                if (!context.Response.HasStarted)
                {
                    throw new UnauthorizedException("Authentication Failed.");
                }

                return Task.CompletedTask;
            },
            OnForbidden = _ => throw new ForbiddenException("You are not authorized to access this resource."),
            OnMessageReceived = context =>
            {
                StringValues accessToken = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments($"/{ApiRoutes.ApiPrefix}/{nameof(RealtimeHub)}"))
                {
                    // Read the token out of the query string
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    }
}
