using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Auth;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Web.Authentication;
using AditiKraft.Krafter.Backend.Web.Configuration;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class JwtSigningKeyEncodingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothHostsAcceptIssuedAndRefreshedTokensWithANonAsciiSigningKey(bool splitUiOptions)
    {
        // Public synthetic test material, including characters whose ASCII and UTF-8 bytes differ.
        var settings = Options.Create(new JwtSettings
        {
            Key = "SyntheticEncodingRegressionSigningMaterial-2026-é-π",
            TokenExpirationInMinutes = 10, RefreshTokenExpirationInDays = 7
        });
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        ApplicationUser user = await scope.AddUserAsync();
        var tokens = new TokenService(scope.Users, scope.Db, scope.Services.GetRequiredService<IUserService>(),
            settings, scope.Services.GetRequiredService<IOptions<SecuritySettings>>());
        TokenResponse issued = await tokens.GenerateTokensAndUpdateUser(user, "encoding-regression");

        var options = new JwtBearerOptions();
        if (splitUiOptions)
            new ConfigureBlazorJwtBearerOptions(settings).Configure(JwtBearerDefaults.AuthenticationScheme, options);
        else
            new ConfigureJwtBearerOptions(settings).Configure(JwtBearerDefaults.AuthenticationScheme, options);

        var validator = new JwtSecurityTokenHandler { MapInboundClaims = options.MapInboundClaims };
        ClaimsPrincipal principal = validator.ValidateToken(issued.Token, options.TokenValidationParameters, out _);
        Assert.Equal(user.Id, principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.True(await scope.Sessions.ValidateAsync(principal));

        var refresh = new RefreshToken.Handler(scope.Users, scope.Db, tokens, settings, scope.Sessions,
            scope.Services.GetRequiredService<ITenantGetterService>());
        var result = await refresh.RefreshTokenAsync(new()
        {
            Token = issued.Token, RefreshToken = issued.RefreshToken
        }, "encoding-regression", CancellationToken.None);
        Assert.False(result.IsError);
        Assert.NotNull(result.Data);
        ClaimsPrincipal refreshed = validator.ValidateToken(result.Data.Token, options.TokenValidationParameters, out _);
        Assert.Equal(user.Id, refreshed.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.True(await scope.Sessions.ValidateAsync(refreshed));
    }
}
