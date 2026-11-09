using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Auth;
using AditiKraft.Krafter.Backend.Web.Authentication;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class TwoFactorSessionTests
{
    [Fact]
    public async Task RefreshRemainsBoundToTheSameUserAfterAnEmailChange()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        var login = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        TokenResponse original = login.Data!.Tokens!;
        user.Email = "updated@example.com";
        user.UserName = user.Email;
        Assert.True((await scope.Users.UpdateAsync(user)).Succeeded);
        var handler = RefreshHandler(scope);
        var response = await handler.RefreshTokenAsync(new() { Token = original.Token, RefreshToken = original.RefreshToken }, "test", CancellationToken.None);
        Assert.False(response.IsError);
        Assert.Equal(user.Id, MfaHarness.Principal(response.Data!).FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(user.Email, MfaHarness.Principal(response.Data!).FindFirstValue(ClaimTypes.Email));
    }

    [Fact]
    public async Task RefreshDoesNotUseAnotherAccountsCredentialAfterEmailReassignment()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var first = await scope.AddUserAsync();
        var login = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        TokenResponse original = login.Data!.Tokens!;
        first.Email = "updated@example.com";
        first.UserName = first.Email;
        Assert.True((await scope.Users.UpdateAsync(first)).Succeeded);
        var second = await scope.AddUserAsync();
        TokenResponse secondTokens = await scope.Tokens.GenerateTokensAndUpdateUser(second, "test");
        var handler = RefreshHandler(scope);
        var response = await handler.RefreshTokenAsync(new() { Token = original.Token, RefreshToken = secondTokens.RefreshToken }, "test", CancellationToken.None);
        Assert.True(response.IsError);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task GooglePrimaryAuthenticationTimeSurvivesRefreshAndCannotAuthorizeStaleEnrollment()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        Assert.True((await scope.Users.RemovePasswordAsync(user)).Succeeded);
        long originalTime = harness.Clock.GetUtcNow().ToUnixTimeSeconds();
        var token = await scope.Tokens.GenerateTokensAndUpdateUser(user, "test", originalTime, "google");
        harness.Clock.Advance(TimeSpan.FromMinutes(6));
        var handler = RefreshHandler(scope);
        var refreshed = await handler.RefreshTokenAsync(new() { Token = token.Token, RefreshToken = token.RefreshToken }, "test", CancellationToken.None);
        Assert.False(refreshed.IsError);
        var options = new JwtBearerOptions();
        new ConfigureJwtBearerOptions(Options.Create(Settings())).Configure(JwtBearerDefaults.AuthenticationScheme, options);
        var validator = new JwtSecurityTokenHandler { MapInboundClaims = options.MapInboundClaims };
        ClaimsPrincipal principal = validator.ValidateToken(refreshed.Data!.Token, options.TokenValidationParameters, out _);
        Assert.Equal("google", principal.FindFirstValue(AppClaimTypes.AuthenticationMethod));
        Assert.Equal(originalTime.ToString(), principal.FindFirstValue(AppClaimTypes.AuthenticationTime));
        Assert.True((await scope.Mfa.BeginEnrollmentAsync(user.Id, new(), principal)).IsError);
    }

    [Fact]
    public async Task RevokedRefreshCredentialCannotCreateANewSession()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        var initial = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        var original = initial.Data!.Tokens!;
        await harness.EnrollAsync(scope, user);
        var handler = RefreshHandler(scope);
        var response = await handler.RefreshTokenAsync(new() { Token = original.Token, RefreshToken = original.RefreshToken }, "test", CancellationToken.None);
        Assert.True(response.IsError);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task RefreshPreservesMfaProofAndRejectsThePreviouslyUsedRefreshCredential()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        (_, IReadOnlyList<string> codes) = await harness.EnrollAsync(scope, user);
        var primary = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        var login = await scope.Mfa.CompleteLoginAsync(new()
        { ChallengeId = primary.Data!.Challenge!.Id, Code = codes[0], UseRecoveryCode = true }, "test");
        TokenResponse original = login.Data!;
        var request = new RefreshTokenRequest { Token = original.Token, RefreshToken = original.RefreshToken };
        RefreshToken.Handler handler = RefreshHandler(scope);
        var refreshed = await handler.RefreshTokenAsync(request, "test", CancellationToken.None);
        Assert.False(refreshed.IsError);
        ClaimsPrincipal before = MfaHarness.Principal(original);
        ClaimsPrincipal after = MfaHarness.Principal(refreshed.Data!);
        Assert.Equal("true", after.FindFirstValue(AppClaimTypes.Mfa));
        foreach (string claim in new[] { AppClaimTypes.AuthenticationTime, AppClaimTypes.AuthenticationMethod,
                     AppClaimTypes.AuthenticationVersion, AppClaimTypes.TenantId })
        {
            Assert.Equal(before.FindFirstValue(claim), after.FindFirstValue(claim));
        }
        Assert.Equal(401, (await handler.RefreshTokenAsync(request, "test", CancellationToken.None)).StatusCode);
        Assert.Equal(refreshed.Data!.RefreshToken, (await scope.Db.UserRefreshTokens.SingleAsync()).RefreshToken);
    }

    [Fact]
    public async Task RefreshDoesNotDiscardUnsavedCallerChanges()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        var login = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        TokenResponse original = login.Data!.Tokens!;
        user.FirstName = "Unsaved caller change";
        await Assert.ThrowsAsync<InvalidOperationException>(() => RefreshHandler(scope).RefreshTokenAsync(
            new() { Token = original.Token, RefreshToken = original.RefreshToken }, "test", CancellationToken.None));
        Assert.True(scope.Db.ChangeTracker.HasChanges());
        Assert.Equal("Unsaved caller change", user.FirstName);
        using MfaScope check = harness.Scope();
        Assert.NotEqual(user.FirstName, (await check.Users.FindByIdAsync(user.Id))!.FirstName);
        Assert.Equal(original.RefreshToken, (await check.Db.UserRefreshTokens.SingleAsync()).RefreshToken);
    }

    private static RefreshToken.Handler RefreshHandler(MfaScope scope) => new(
        scope.Users, scope.Db, scope.Tokens, Options.Create(Settings()), scope.Sessions,
        scope.Services.GetRequiredService<ITenantGetterService>());

    private static JwtSettings Settings() => new()
    {
        Key = "test-signing-key-for-mfa-tests-only-at-least-32-bytes",
        TokenExpirationInMinutes = 60,
        RefreshTokenExpirationInDays = 7
    };
}
