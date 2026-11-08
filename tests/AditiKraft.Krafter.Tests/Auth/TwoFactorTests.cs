using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web.Configuration;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class TwoFactorTests
{
    [Fact]
    public async Task EnrollmentIsUnavailableWithoutApprovedKeyStorage()
    {
        using var harness = new MfaHarness(enrollmentAllowed: false);
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        var response = await scope.Mfa.BeginEnrollmentAsync(user.Id, new() { Password = MfaHarness.Password },
            await harness.ManagementPrincipalAsync(scope, user.Id));
        Assert.Equal(503, response.StatusCode);
        Assert.Empty(await scope.Db.TwoFactorChallenges.ToListAsync());
    }

    [Fact]
    public async Task TokenServiceCannotIssueCredentialsForAnEnabledAccountWithoutProof()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        await harness.EnrollAsync(scope, user);
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Tokens.GenerateTokensAndUpdateUser(user, "test"));
    }

    [Fact]
    public async Task StaleChallengesAndSecondFactorLockoutFailClosed()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        (_, IReadOnlyList<string> codes) = await harness.EnrollAsync(scope, user);
        var stale = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        user.AuthenticationVersion++;
        Assert.True((await scope.Users.UpdateAsync(user)).Succeeded);
        Assert.True((await scope.Mfa.CompleteLoginAsync(new() { ChallengeId = stale.Data!.Challenge!.Id, Code = codes[0], UseRecoveryCode = true }, "test")).IsError);
        var login = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        for (int i = 0; i < 5; i++)
            Assert.True((await scope.Mfa.CompleteLoginAsync(new() { ChallengeId = login.Data!.Challenge!.Id, Code = "invalid" }, "test")).IsError);
        Assert.True(await scope.Users.IsLockedOutAsync(user));
        Assert.True((await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test")).IsError);
    }

    [Fact]
    public async Task GoogleOnlyEnrollmentRequiresRecentPrimaryAuthentication()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        Assert.True((await scope.Users.RemovePasswordAsync(user)).Succeeded);
        Assert.True((await scope.Mfa.BeginEnrollmentAsync(user.Id, new(), new())).IsError);
        var login = await scope.Mfa.ExternalLoginAsync(user.Id, "test");
        ClaimsPrincipal principal = MfaHarness.Principal(login.Data!.Tokens!);
        Assert.False((await scope.Mfa.BeginEnrollmentAsync(user.Id, new(), principal)).IsError);
        harness.Clock.Advance(TimeSpan.FromMinutes(6));
        Assert.True((await scope.Mfa.BeginEnrollmentAsync(user.Id, new(), principal)).IsError);
    }

    [Fact]
    public async Task EnrollmentProtectsSecretsAndRevokesThePriorSession()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        ApplicationUser user = await scope.AddUserAsync();
        Response<LoginResponse> initial = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        Assert.NotNull(initial.Data!.Tokens);
        ClaimsPrincipal principal = MfaHarness.Principal(initial.Data.Tokens!);
        var setup = await scope.Mfa.BeginEnrollmentAsync(user.Id, new() { Password = MfaHarness.Password }, principal);
        Assert.False(setup.IsError);
        Assert.Contains("<svg", setup.Data!.QrCodeSvg);
        TwoFactorChallenge pending = await scope.Db.TwoFactorChallenges.SingleAsync();
        Assert.DoesNotContain(setup.Data.SharedKey, pending.ProtectedKey!);
        Assert.NotEqual(setup.Data.EnrollmentId, pending.Id);
        var confirmation = await scope.Mfa.ConfirmEnrollmentAsync(user.Id, new()
        { ChallengeId = setup.Data.EnrollmentId, Code = harness.Code(setup.Data.SharedKey) }, principal);
        Assert.False(confirmation.IsError);
        Assert.Equal(10, confirmation.Data!.Codes.Count);
        Assert.All(await scope.Db.TwoFactorRecoveryCodes.ToListAsync(), x =>
            Assert.DoesNotContain(confirmation.Data.Codes, value => value.Replace("-", "") == x.CodeHash));
        Assert.False(await scope.Sessions.ValidateAsync(MfaHarness.Principal(initial.Data.Tokens!)));
        Assert.Null((await scope.Db.UserRefreshTokens.SingleAsync()).RefreshToken);
    }

    [Fact]
    public async Task PendingLoginHasNoCredentialsAndUsedEnrollmentCodeCannotLogin()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        ApplicationUser user = await scope.AddUserAsync();
        (string secret, _) = await harness.EnrollAsync(scope, user);
        var start = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        Assert.True(start.Data!.RequiresTwoFactor);
        Assert.Null(start.Data.Tokens);
        Assert.Empty(await scope.Db.UserRefreshTokens.ToListAsync());
        var replay = await scope.Mfa.CompleteLoginAsync(new() { ChallengeId = start.Data.Challenge!.Id, Code = harness.Code(secret) }, "test");
        Assert.True(replay.IsError);
        harness.Clock.Advance(TimeSpan.FromSeconds(30));
        var success = await scope.Mfa.CompleteLoginAsync(new() { ChallengeId = start.Data.Challenge.Id, Code = harness.Code(secret) }, "test");
        Assert.False(success.IsError);
        Assert.True(await scope.Sessions.ValidateAsync(MfaHarness.Principal(success.Data!)));
        var consumed = await scope.Mfa.CompleteLoginAsync(new() { ChallengeId = start.Data.Challenge.Id, Code = harness.Code(secret) }, "test");
        Assert.True(consumed.IsError);
    }

    [Fact]
    public async Task RecoveryCodesAreOneTimeAndRegenerationInvalidatesOldCodesAndSessions()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        ApplicationUser user = await scope.AddUserAsync();
        (_, IReadOnlyList<string> codes) = await harness.EnrollAsync(scope, user);
        var login = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        var completed = await scope.Mfa.CompleteLoginAsync(new() { ChallengeId = login.Data!.Challenge!.Id, Code = codes[0], UseRecoveryCode = true }, "test");
        Assert.False(completed.IsError);
        Assert.Equal(9, (await scope.Mfa.StatusAsync(user.Id)).Data!.RecoveryCodesRemaining);
        var regenerate = await scope.Mfa.RegenerateAsync(user.Id, new() { Code = codes[1], UseRecoveryCode = true },
            MfaHarness.Principal(completed.Data!));
        Assert.False(regenerate.IsError);
        Assert.Equal(10, regenerate.Data!.Codes.Count);
        Assert.False(await scope.Sessions.ValidateAsync(MfaHarness.Principal(completed.Data!)));
        var retry = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        Assert.True((await scope.Mfa.CompleteLoginAsync(new() { ChallengeId = retry.Data!.Challenge!.Id, Code = codes[2], UseRecoveryCode = true }, "test")).IsError);
    }

    [Fact]
    public async Task ChallengesExpireAndCannotCrossTenantBoundaries()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope("tenant-a");
        ApplicationUser user = await scope.AddUserAsync();
        (string secret, _) = await harness.EnrollAsync(scope, user);
        var login = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        using MfaScope other = harness.Scope("tenant-b");
        var request = new TwoFactorRequest { ChallengeId = login.Data!.Challenge!.Id, Code = harness.Code(secret) };
        Assert.True((await other.Mfa.CompleteLoginAsync(request, "test")).IsError);
        harness.Clock.Advance(TimeSpan.FromMinutes(6));
        Assert.True((await scope.Mfa.CompleteLoginAsync(request, "test")).IsError);
    }

    [Fact]
    public async Task FailedPrimaryAndSecondFactorsEnforceAccountLockout()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        ApplicationUser user = await scope.AddUserAsync();
        for (int i = 0; i < 5; i++)
            Assert.True((await scope.Mfa.PasswordLoginAsync(new() { Email = user.Email!, Password = "incorrect" }, "test")).IsError);
        Assert.True(await scope.Users.IsLockedOutAsync(user));
        Assert.True((await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test")).IsError);
    }

    [Fact]
    public async Task GoogleLoginRequiresLocalMfaAndDisableNeedsAnEnrolledFactor()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        ApplicationUser user = await scope.AddUserAsync();
        (_, IReadOnlyList<string> codes) = await harness.EnrollAsync(scope, user);
        Assert.True((await scope.Mfa.ExternalLoginAsync(user.Id, "test")).Data!.RequiresTwoFactor);
        ClaimsPrincipal principal = await harness.ManagementPrincipalAsync(scope, user.Id);
        Assert.True((await scope.Mfa.DisableAsync(user.Id, new() { Password = MfaHarness.Password }, principal)).IsError);
        Assert.False((await scope.Mfa.DisableAsync(user.Id, new() { Code = codes[0], UseRecoveryCode = true }, principal)).IsError);
        Assert.False((await scope.Mfa.StatusAsync(user.Id)).Data!.IsEnabled);
        Assert.NotNull((await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test")).Data!.Tokens);
    }
}

internal sealed class MfaHarness : IDisposable
{
    public const string Password = "TestPassword1!";
    private readonly ServiceProvider provider;
    public MfaClock Clock { get; } = new();
    public MfaHarness(string? postgres = null, bool enrollmentAllowed = true,
        Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor? interceptor = null)
    {
        string database = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<CurrentTenantService>();
        services.AddScoped<ITenantGetterService>(p => p.GetRequiredService<CurrentTenantService>());
        services.AddScoped<ITenantSetterService>(p => p.GetRequiredService<CurrentTenantService>());
        services.AddDbContext<TenantDbContext>(o => o.UseInMemoryDatabase(database + "-tenants"));
        services.AddScoped<ITenantFinderService, AditiKraft.Krafter.Backend.Infrastructure.Persistence.Tenants.TenantFinderService>();
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (postgres is null) options.UseInMemoryDatabase(database);
            else options.UseNpgsql(postgres);
            if (interceptor is not null) options.AddInterceptors(interceptor);
        });
        services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        services.Configure<AditiKraft.Krafter.Contracts.Common.Auth.JwtSettings>(o =>
        { o.Key = "test-signing-key-for-mfa-tests-only-at-least-32-bytes"; o.TokenExpirationInMinutes = 60; o.RefreshTokenExpirationInDays = 7; });
        services.Configure<SecuritySettings>(_ => { });
        services.Configure<TwoFactorProtectionOptions>(o => o.EnrollmentAllowed = enrollmentAllowed);
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<TwoFactorService>();
        services.AddScoped<SessionValidator>();
        provider = services.BuildServiceProvider();
    }
    public IServiceProvider Services => provider;
    public MfaScope Scope(string tenantId = "test") => new(provider.CreateScope(), tenantId);
    public string Code(string secret) => new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(Clock.GetUtcNow().UtcDateTime);
    public async Task<ClaimsPrincipal> ManagementPrincipalAsync(MfaScope scope, string userId)
    {
        // Model middleware's verified claims without creating or rotating a refresh credential.
        ApplicationUser user = await scope.Db.Users.AsNoTracking().SingleAsync(x => x.Id == userId);
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(AppClaimTypes.TenantId, user.TenantId),
            new Claim(AppClaimTypes.AuthenticationVersion, user.AuthenticationVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(AppClaimTypes.AuthenticationTime, Clock.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(AppClaimTypes.AuthenticationMethod, "pwd"),
            new Claim(AppClaimTypes.Mfa, user.TwoFactorEnabled ? "true" : "false")
        }, "test"));
    }
    public async Task<(string Secret, IReadOnlyList<string> Codes)> EnrollAsync(MfaScope scope, ApplicationUser user)
    {
        ClaimsPrincipal principal = await ManagementPrincipalAsync(scope, user.Id);
        var setup = await scope.Mfa.BeginEnrollmentAsync(user.Id, new() { Password = Password }, principal);
        Assert.False(setup.IsError);
        var confirmed = await scope.Mfa.ConfirmEnrollmentAsync(user.Id,
            new() { ChallengeId = setup.Data!.EnrollmentId, Code = Code(setup.Data.SharedKey) }, principal);
        Assert.False(confirmed.IsError);
        return (setup.Data.SharedKey, confirmed.Data!.Codes);
    }
    public static TokenRequest PasswordRequest() => new() { Email = "user@example.com", Password = Password };
    public static ClaimsPrincipal Principal(TokenResponse response) => new(new ClaimsIdentity(new JwtSecurityTokenHandler().ReadJwtToken(response.Token).Claims, "test"));
    public void Dispose() => provider.Dispose();
}

internal sealed class MfaScope : IDisposable
{
    private readonly IServiceScope scope;
    public MfaScope(IServiceScope scope, string tenantId)
    {
        this.scope = scope;
        scope.ServiceProvider.GetRequiredService<CurrentTenantService>().SetTenant(new CurrentTenantDetails
        { Id = tenantId, Identifier = tenantId, Name = tenantId, TenantLink = "https://" + tenantId + ".example.com", IsActive = true, ValidUpto = DateTime.MaxValue });
    }
    public IServiceProvider Services => scope.ServiceProvider;
    public ApplicationDbContext Db => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    public UserManager<ApplicationUser> Users => scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    public TwoFactorService Mfa => scope.ServiceProvider.GetRequiredService<TwoFactorService>();
    public ITokenService Tokens => scope.ServiceProvider.GetRequiredService<ITokenService>();
    public SessionValidator Sessions => scope.ServiceProvider.GetRequiredService<SessionValidator>();
    public async Task<ApplicationUser> AddUserAsync()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "user@example.com", Email = "user@example.com", IsActive = true };
        IdentityResult result = await Users.CreateAsync(user, MfaHarness.Password);
        Assert.True(result.Succeeded, string.Join(';', result.Errors.Select(e => e.Description)));
        return user;
    }
    public void Dispose() => scope.Dispose();
}

internal sealed class MfaClock : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan elapsed) => now += elapsed;
}
