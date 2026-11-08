using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Tests.Auth;

// Run with a dedicated empty PostgreSQL database, never an application's database.
public sealed class PostgresMfaFactAttribute : FactAttribute
{
    public PostgresMfaFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")))
            Skip = "Set KRAFTER_TEST_POSTGRES to a dedicated PostgreSQL test database.";
    }
}

[Collection("MFA PostgreSQL")]
public sealed class TwoFactorPostgresTests
{
    [PostgresMfaFact]
    public Task AuthenticationFailureCannotRestoreAConcurrentlyDeletedAccount() =>
        UserDeletionRaceTests.VerifyAsync(Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!, "wrong-password");

    [PostgresMfaFact]
    public async Task ConcurrentCompletionInSeparateScopesIssuesCredentialsOnce()
    {
        using var harness = CreateHarness();
        string tenant = Guid.NewGuid().ToString();
        using MfaScope scope = harness.Scope(tenant);
        await scope.Db.Database.MigrateAsync();
        var user = await scope.AddUserAsync();
        (string secret, _) = await harness.EnrollAsync(scope, user);
        harness.Clock.Advance(TimeSpan.FromSeconds(30));
        var start = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        var request = new TwoFactorRequest { ChallengeId = start.Data!.Challenge!.Id, Code = harness.Code(secret) };
        using MfaScope first = harness.Scope(tenant);
        using MfaScope second = harness.Scope(tenant);
        var results = await Task.WhenAll(first.Mfa.CompleteLoginAsync(request, "first"), second.Mfa.CompleteLoginAsync(request, "second"));
        Assert.Single(results.Where(x => !x.IsError));
        Assert.Single(results.Where(x => x.IsError));
        Assert.Equal(1, await scope.Db.UserRefreshTokens.AsNoTracking().CountAsync(x => x.UserId == user.Id));
    }

    [PostgresMfaFact]
    public async Task RecoveryCodeCannotCompleteLoginAndRegenerateAtTheSameTime()
    {
        using var harness = CreateHarness();
        string tenant = Guid.NewGuid().ToString();
        using MfaScope scope = harness.Scope(tenant);
        await scope.Db.Database.MigrateAsync();
        var user = await scope.AddUserAsync();
        (_, IReadOnlyList<string> codes) = await harness.EnrollAsync(scope, user);
        var start = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        using MfaScope first = harness.Scope(tenant);
        using MfaScope second = harness.Scope(tenant);
        var login = first.Mfa.CompleteLoginAsync(new() { ChallengeId = start.Data!.Challenge!.Id, Code = codes[0], UseRecoveryCode = true }, "first");
        var regenerate = second.Mfa.RegenerateAsync(user.Id, new() { Code = codes[0], UseRecoveryCode = true }, new());
        await Task.WhenAll(login, regenerate);
        Assert.NotEqual(login.Result.IsError, regenerate.Result.IsError);
    }

    [PostgresMfaFact]
    public async Task TwoTenantsWithTheSameEmailCannotShareChallengesOrCodes()
    {
        using var harness = CreateHarness();
        using MfaScope first = harness.Scope(Guid.NewGuid().ToString());
        await first.Db.Database.MigrateAsync();
        using MfaScope second = harness.Scope(Guid.NewGuid().ToString());
        var alice = await first.AddUserAsync();
        var bob = await second.AddUserAsync();
        (_, IReadOnlyList<string> codes) = await harness.EnrollAsync(first, alice);
        await harness.EnrollAsync(second, bob);
        var start = await first.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        var result = await second.Mfa.CompleteLoginAsync(new() { ChallengeId = start.Data!.Challenge!.Id, Code = codes[0], UseRecoveryCode = true }, "test");
        Assert.True(result.IsError);
    }

    private static MfaHarness CreateHarness() => new(Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!);
}

[CollectionDefinition("MFA PostgreSQL", DisableParallelization = true)]
public sealed class MfaPostgresCollection;
