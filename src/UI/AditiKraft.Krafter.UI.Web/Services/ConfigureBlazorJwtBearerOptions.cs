using System.Security.Claims;
using System.Text;
using AditiKraft.Krafter.Contracts.Common.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AditiKraft.Krafter.UI.Web.Services;

/// <summary>
/// Configures JWT bearer options for the Blazor split-host UI.
/// Mirrors the backend's <c>ConfigureJwtBearerOptions</c> for token validation,
/// but uses <see cref="BlazorJwtBearerEvents"/> for cookie-aware SSR event handling.
/// </summary>
public class ConfigureBlazorJwtBearerOptions(IOptions<JwtSettings> jwtSettings)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtSettings _jwtSettings = jwtSettings.Value;

    public void Configure(JwtBearerOptions options) => Configure(string.Empty, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        byte[] key = Encoding.ASCII.GetBytes(_jwtSettings.Key);

        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new BlazorJwtBearerEvents(BlazorHostingMode.SplitHost);
    }
}
