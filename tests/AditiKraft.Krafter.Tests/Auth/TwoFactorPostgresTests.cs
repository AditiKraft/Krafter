using System.Net;
using AditiKraft.Krafter.Backend.Features.Auth;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

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
        Assert.Single(results, x => !x.IsError);
        Assert.Single(results, x => x.IsError);
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

    [PostgresMfaFact]
    public async Task CancelingAnAdvisoryLockWaitDoesNotConsumeProofAndAllowsRetryAsync()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!)
        {
            ApplicationName = "krafter-mfa-cancellation-" + Guid.NewGuid().ToString("N")
        };
        using var harness = new MfaHarness(connectionString.ConnectionString);
        string tenantId = Guid.NewGuid().ToString();
        using MfaScope setup = harness.Scope(tenantId);
        await setup.Db.Database.MigrateAsync();
        var user = await setup.AddUserAsync();
        (_, IReadOnlyList<string> codes) = await harness.EnrollAsync(setup, user);
        var login = await setup.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        var proof = new TwoFactorRequest { ChallengeId = login.Data!.Challenge!.Id, Code = codes[0], UseRecoveryCode = true };
        using MfaScope holder = harness.Scope(tenantId);
        await using var transaction = await holder.Db.Database.BeginTransactionAsync();
        string lockKey = $"mfa:{tenantId}:{user.Id}";
        await holder.Db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))");
        using MfaScope waiting = harness.Scope(tenantId);
        using var cancellation = new CancellationTokenSource();
        Task canceled = waiting.Mfa.CompleteLoginAsync(proof, "test", cancellation.Token);
        await using var observer = new NpgsqlConnection(connectionString.ConnectionString);
        await observer.OpenAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (SELECT FROM pg_stat_activity
            WHERE application_name = @name AND wait_event_type = 'Lock' AND wait_event = 'advisory')
            """, observer);
        command.Parameters.AddWithValue("name", connectionString.ApplicationName);
        while (!(bool)(await command.ExecuteScalarAsync(deadline.Token))!)
        {
            await Task.Delay(25, deadline.Token);
        }
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled.WaitAsync(TimeSpan.FromSeconds(5)));
        using MfaScope check = harness.Scope(tenantId);
        Assert.Equal(10, await check.Db.TwoFactorRecoveryCodes.CountAsync());
        Assert.Equal(0, (await check.Db.TwoFactorChallenges.SingleAsync()).Attempts);
        Assert.Equal(0, await check.Db.UserRefreshTokens.CountAsync(token => token.UserId == user.Id));
        await transaction.RollbackAsync();
        Assert.False((await check.Mfa.CompleteLoginAsync(proof, "retry")).IsError);
    }

    [PostgresMfaFact]
    public async Task CancellationAfterSavingCredentialsRollsBackTheWholeTransitionAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var interceptor = new CancelAfterCredentialSave(cancellation);
        using var harness = new MfaHarness(Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!, interceptor: interceptor);
        string tenantId = Guid.NewGuid().ToString();
        using MfaScope setup = harness.Scope(tenantId);
        await setup.Db.Database.MigrateAsync();
        var user = await setup.AddUserAsync();
        (_, IReadOnlyList<string> codes) = await harness.EnrollAsync(setup, user);
        var login = await setup.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        var proof = new TwoFactorRequest { ChallengeId = login.Data!.Challenge!.Id, Code = codes[0], UseRecoveryCode = true };
        interceptor.Enabled = true;
        using MfaScope operation = harness.Scope(tenantId);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.Mfa.CompleteLoginAsync(proof, "test", cancellation.Token));
        Assert.True(interceptor.CanceledAfterSave);
        using MfaScope check = harness.Scope(tenantId);
        Assert.Equal(10, await check.Db.TwoFactorRecoveryCodes.CountAsync());
        Assert.Single(await check.Db.TwoFactorChallenges.ToListAsync());
        Assert.Equal(0, await check.Db.UserRefreshTokens.CountAsync(token => token.UserId == user.Id));
        Assert.False((await check.Mfa.CompleteLoginAsync(proof, "retry")).IsError);
    }

    private sealed class CancelAfterCredentialSave(CancellationTokenSource cancellation) : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public bool CanceledAfterSave { get; private set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context is AditiKraft.Krafter.Backend.Infrastructure.Persistence.ApplicationDbContext db &&
                db.UserRefreshTokens.Local.Any(token => token.RefreshToken is not null))
            {
                Enabled = false;
                CanceledAfterSave = true;
                cancellation.Cancel();
            }
            return ValueTask.FromResult(result);
        }
    }

    [PostgresMfaFact]
    public async Task CanceledGoogleProvisioningCannotLeaveAnAccountWithoutItsBasicRoleAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var interceptor = new CancelAfterGoogleUserSave(cancellation);
        using var harness = new MfaHarness(Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!, interceptor: interceptor);
        string tenantId = Guid.NewGuid().ToString();
        using MfaScope setup = harness.Scope(tenantId);
        await setup.Db.Database.MigrateAsync();
        RoleManager<ApplicationRole> roles = setup.Services.GetRequiredService<RoleManager<ApplicationRole>>();
        var basic = new ApplicationRole { Id = Guid.NewGuid().ToString(), Name = RoleConstants.Basic };
        Assert.True((await roles.CreateAsync(basic)).Succeeded);
        interceptor.Enabled = true;
        using MfaScope operation = harness.Scope(tenantId);
        using var transport = new GoogleTransport();
        using var http = new HttpClient(transport);
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Urls:RootUiUrl"] = "https://app.example.test",
            ["Authentication:Google:ClientId"] = "test-client",
            ["Authentication:Google:ClientSecret"] = "test-secret"
        }).Build();
        var google = new ExternalAuth.GoogleAuthClient(http, configuration, NullLogger<ExternalAuth.GoogleAuthClient>.Instance);
        var handler = new ExternalAuth.Handler(operation.Mfa, operation.Db, operation.Users,
            operation.Services.GetRequiredService<RoleManager<ApplicationRole>>(), google);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.GetTokenAsync(new() { Code = "test-code" }, cancellation.Token));
        Assert.True(interceptor.CanceledAfterSave);
        using MfaScope check = harness.Scope(tenantId);
        Assert.Equal(0, await check.Db.Users.CountAsync(user => user.Email == GoogleTransport.Email));
        Assert.Equal(0, await check.Db.UserRoles.CountAsync(link => link.RoleId == basic.Id));
        var retry = new ExternalAuth.Handler(check.Mfa, check.Db, check.Users,
            check.Services.GetRequiredService<RoleManager<ApplicationRole>>(), google);
        var result = await retry.GetTokenAsync(new() { Code = "test-code" }, CancellationToken.None);
        Assert.False(result.IsError);
        var created = await check.Db.Users.SingleAsync(user => user.Email == GoogleTransport.Email);
        Assert.True(await check.Db.UserRoles.AnyAsync(link => link.UserId == created.Id && link.RoleId == basic.Id));
    }

    private sealed class CancelAfterGoogleUserSave(CancellationTokenSource cancellation) : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public bool CanceledAfterSave { get; private set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context is AditiKraft.Krafter.Backend.Infrastructure.Persistence.ApplicationDbContext db &&
                db.Users.Local.Any(user => user.Email == GoogleTransport.Email))
            {
                Enabled = false;
                CanceledAfterSave = true;
                cancellation.Cancel();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class GoogleTransport : HttpMessageHandler
    {
        public const string Email = "new-google-user@example.test";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.Host == "oauth2.googleapis.com"
                    ? """{"access_token":"test-access-token","id_token":"unused"}"""
                    : """{"email":"new-google-user@example.test","verified_email":true,"given_name":"Test","family_name":"User"}""",
                    System.Text.Encoding.UTF8, "application/json")
            });
    }
}

[CollectionDefinition("MFA PostgreSQL", DisableParallelization = true)]
public sealed class MfaPostgresCollection;
