using System.Data.Common;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Auth;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AditiKraft.Krafter.Tests.Auth;

[Collection("MFA PostgreSQL")]
public sealed class RefreshTokenPostgresTests
{
    [PostgresMfaFact]
    public async Task InFlightRefreshCannotOverwriteTheLoginAfterAnMfaChangeAsync()
    {
        string connectionString = ConnectionString();
        var barrier = new PauseBeforeSave();
        using var harness = new MfaHarness(connectionString, interceptor: barrier);
        string tenantId = Guid.NewGuid().ToString();
        using MfaScope setup = harness.Scope(tenantId);
        await setup.Db.Database.MigrateAsync();
        var user = await setup.AddUserAsync();
        (_, IReadOnlyList<string> codes) = await harness.EnrollAsync(setup, user);
        TokenResponse original = await LoginAsync(setup, codes[0], CancellationToken.None);
        using MfaScope refreshing = harness.Scope(tenantId);
        using MfaScope changing = harness.Scope(tenantId);
        using MfaScope signingIn = harness.Scope(tenantId);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        barrier.Target = refreshing.Db;
        Task<Response<TokenResponse>> refresh = RefreshHandler(refreshing).RefreshTokenAsync(
            Request(original), "old-refresh", deadline.Token);
        try
        {
            // At this barrier the old token/refresh proof has passed validation and the new
            // refresh value is tracked, but the credential update has not reached PostgreSQL.
            await barrier.Paused.Task.WaitAsync(deadline.Token);
            Task<Response<RecoveryCodesResponse>> change = changing.Mfa.RegenerateAsync(user.Id,
                new() { Code = codes[1], UseRecoveryCode = true }, MfaHarness.Principal(original), deadline.Token);
            TokenResponse? fresh = null;
            if (await WaitForAccountLockOrCompletionAsync(change, connectionString, deadline.Token))
            {
                // With serialization the MFA change must wait for this refresh to commit.
                barrier.Resume.TrySetResult();
            }
            else
            {
                // Without serialization, reproduce the bug: commit the MFA change and new
                // login before allowing the old refresh's stale write to reach PostgreSQL.
                Response<RecoveryCodesResponse> changed = await change;
                Assert.False(changed.IsError);
                fresh = await LoginAsync(signingIn, changed.Data!.Codes[0], deadline.Token);
                barrier.Resume.TrySetResult();
            }

            Response<TokenResponse> refreshed = await refresh.WaitAsync(deadline.Token);
            Response<RecoveryCodesResponse> replacement = await change.WaitAsync(deadline.Token);
            Assert.False(refreshed.IsError);
            Assert.False(replacement.IsError);
            fresh ??= await LoginAsync(signingIn, replacement.Data!.Codes[0], deadline.Token);
            using MfaScope check = harness.Scope(tenantId);
            Assert.False(await check.Sessions.ValidateAsync(MfaHarness.Principal(refreshed.Data!), deadline.Token));
            Assert.Equal(fresh.RefreshToken, (await check.Db.UserRefreshTokens.SingleAsync(
                x => x.UserId == user.Id, deadline.Token)).RefreshToken);
            Assert.False((await RefreshHandler(check).RefreshTokenAsync(Request(fresh), "fresh-login", deadline.Token)).IsError);
        }
        finally
        {
            barrier.Resume.TrySetResult();
        }
    }

    [PostgresMfaFact]
    public async Task ConcurrentRefreshesCanRotateTheSameCredentialOnlyOnceAsync()
    {
        string connectionString = ConnectionString();
        var barrier = new PauseBeforeSave();
        using var harness = new MfaHarness(connectionString, interceptor: barrier);
        string tenantId = Guid.NewGuid().ToString();
        using MfaScope setup = harness.Scope(tenantId);
        await setup.Db.Database.MigrateAsync();
        var user = await setup.AddUserAsync();
        TokenResponse original = (await setup.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test")).Data!.Tokens!;
        using MfaScope first = harness.Scope(tenantId);
        using MfaScope second = harness.Scope(tenantId);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        barrier.Target = first.Db;
        Task<Response<TokenResponse>> one = RefreshHandler(first).RefreshTokenAsync(Request(original), "first", deadline.Token);
        try
        {
            await barrier.Paused.Task.WaitAsync(deadline.Token);
            Task<Response<TokenResponse>> two = RefreshHandler(second).RefreshTokenAsync(Request(original), "second", deadline.Token);
            await WaitForAccountLockOrCompletionAsync(two, connectionString, deadline.Token);
            barrier.Resume.TrySetResult();
            Response<TokenResponse>[] results = await Task.WhenAll(one, two).WaitAsync(deadline.Token);
            Response<TokenResponse> winner = Assert.Single(results, result => !result.IsError);
            Assert.Equal(401, Assert.Single(results, result => result.IsError).StatusCode);
            using MfaScope check = harness.Scope(tenantId);
            Assert.Equal(winner.Data!.RefreshToken, (await check.Db.UserRefreshTokens.SingleAsync(
                x => x.UserId == user.Id, deadline.Token)).RefreshToken);
        }
        finally
        {
            barrier.Resume.TrySetResult();
        }
    }

    [PostgresMfaFact]
    public async Task QueuedRefreshReloadsStateAndCannotPromoteAnOldSessionAfterMfaChangesAsync()
    {
        var barrier = new PauseBeforeAccountLock();
        using var harness = new MfaHarness(ConnectionString(), interceptor: barrier);
        string tenantId = Guid.NewGuid().ToString();
        using MfaScope setup = harness.Scope(tenantId);
        await setup.Db.Database.MigrateAsync();
        var user = await setup.AddUserAsync();
        (_, IReadOnlyList<string> codes) = await harness.EnrollAsync(setup, user);
        TokenResponse original = await LoginAsync(setup, codes[0], CancellationToken.None);
        using MfaScope waiting = harness.Scope(tenantId);
        // Model authentication middleware or earlier request work loading the old account.
        Assert.True(await waiting.Sessions.ValidateAsync(MfaHarness.Principal(original)));
        await waiting.Db.UserRefreshTokens.SingleAsync(x => x.UserId == user.Id);
        barrier.Target = waiting.Db;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Task<Response<TokenResponse>> refresh = RefreshHandler(waiting).RefreshTokenAsync(Request(original), "waiting", deadline.Token);
        try
        {
            await barrier.Paused.Task.WaitAsync(deadline.Token);
            using MfaScope changed = harness.Scope(tenantId);
            var replacement = await changed.Mfa.RegenerateAsync(user.Id,
                new() { Code = codes[1], UseRecoveryCode = true }, MfaHarness.Principal(original), deadline.Token);
            Assert.False(replacement.IsError);
            TokenResponse fresh = await LoginAsync(changed, replacement.Data!.Codes[0], deadline.Token);
            barrier.Resume.TrySetResult();
            Assert.Equal(401, (await refresh.WaitAsync(deadline.Token)).StatusCode);
            using MfaScope check = harness.Scope(tenantId);
            Assert.Equal(fresh.RefreshToken, (await check.Db.UserRefreshTokens.SingleAsync(
                x => x.UserId == user.Id, deadline.Token)).RefreshToken);
            Assert.False((await RefreshHandler(check).RefreshTokenAsync(Request(fresh), "fresh", deadline.Token)).IsError);
        }
        finally
        {
            barrier.Resume.TrySetResult();
        }
    }

    [PostgresMfaFact]
    public async Task CancellationAfterRefreshSaveRollsBackRotationAndAllowsRetryAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var interceptor = new CancelAfterSave(cancellation);
        using var harness = new MfaHarness(ConnectionString(), interceptor: interceptor);
        string tenantId = Guid.NewGuid().ToString();
        using MfaScope setup = harness.Scope(tenantId);
        await setup.Db.Database.MigrateAsync();
        var user = await setup.AddUserAsync();
        TokenResponse original = (await setup.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test")).Data!.Tokens!;
        using MfaScope refreshing = harness.Scope(tenantId);
        interceptor.Target = refreshing.Db;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RefreshHandler(refreshing)
            .RefreshTokenAsync(Request(original), "canceled", cancellation.Token));
        Assert.True(interceptor.CanceledAfterSave);
        using MfaScope check = harness.Scope(tenantId);
        Assert.Equal(original.RefreshToken, (await check.Db.UserRefreshTokens.SingleAsync(x => x.UserId == user.Id)).RefreshToken);
        Assert.False((await RefreshHandler(check).RefreshTokenAsync(Request(original), "retry", CancellationToken.None)).IsError);
    }

    private static RefreshToken.Handler RefreshHandler(MfaScope scope) => new(
        scope.Users, scope.Db, scope.Tokens, scope.Services.GetRequiredService<IOptions<JwtSettings>>(), scope.Sessions,
        scope.Services.GetRequiredService<ITenantGetterService>());

    private static RefreshTokenRequest Request(TokenResponse token) => new() { Token = token.Token, RefreshToken = token.RefreshToken };

    private static async Task<TokenResponse> LoginAsync(MfaScope scope, string recoveryCode, CancellationToken cancellationToken)
    {
        var primary = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "login", cancellationToken);
        Assert.False(primary.IsError);
        var completed = await scope.Mfa.CompleteLoginAsync(new()
        { ChallengeId = primary.Data!.Challenge!.Id, Code = recoveryCode, UseRecoveryCode = true }, "login", cancellationToken);
        Assert.False(completed.IsError);
        return completed.Data!;
    }

    private static string ConnectionString() => new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!)
    {
        ApplicationName = "krafter-refresh-" + Guid.NewGuid().ToString("N")
    }.ConnectionString;

    private static async Task<bool> WaitForAccountLockOrCompletionAsync(Task operation, string connectionString,
        CancellationToken cancellationToken)
    {
        await using var observer = new NpgsqlConnection(connectionString);
        await observer.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (SELECT FROM pg_stat_activity
            WHERE application_name = @name AND wait_event_type = 'Lock' AND wait_event = 'advisory')
            """, observer);
        command.Parameters.AddWithValue("name", new NpgsqlConnectionStringBuilder(connectionString).ApplicationName!);
        while (!operation.IsCompleted)
        {
            if ((bool)(await command.ExecuteScalarAsync(cancellationToken))!) return true;
            await Task.Delay(25, cancellationToken);
        }
        return false;
    }

    private sealed class PauseBeforeSave : SaveChangesInterceptor
    {
        public ApplicationDbContext? Target { get; set; }
        public TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (ReferenceEquals(eventData.Context, Target))
            {
                Target = null;
                Paused.TrySetResult();
                await Resume.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class PauseBeforeAccountLock : DbCommandInterceptor
    {
        public ApplicationDbContext? Target { get; set; }
        public TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (ReferenceEquals(eventData.Context, Target) && command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal))
            {
                Target = null;
                Paused.TrySetResult();
                await Resume.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class CancelAfterSave(CancellationTokenSource cancellation) : SaveChangesInterceptor
    {
        public ApplicationDbContext? Target { get; set; }
        public bool CanceledAfterSave { get; private set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (ReferenceEquals(eventData.Context, Target))
            {
                Target = null;
                CanceledAfterSave = true;
                cancellation.Cancel();
            }
            return ValueTask.FromResult(result);
        }
    }
}
