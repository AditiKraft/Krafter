using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Tests.Auth;
using AditiKraft.Krafter.Tests.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Npgsql;

namespace AditiKraft.Krafter.Tests.Tenants;

// These tests use isolated tenant IDs in the dedicated KRAFTER_TEST_POSTGRES database.
[Collection("MFA PostgreSQL")]
public sealed class DataSeedPostgresTests
{
    [PostgresMfaFact]
    public async Task ConcurrentProvisioningIsAtomicAndCreatesOneOwnerAndOneSetOfClaims()
    {
        string connectionString = ConnectionString();
        var pause = new PauseAfterSave();
        using var harness = new MfaHarness(connectionString, interceptor: pause);
        string tenantId = Guid.NewGuid().ToString();
        using MfaScope setup = harness.Scope(tenantId);
        await PrepareAsync(setup, tenantId);
        using MfaScope first = harness.Scope(tenantId);
        using MfaScope second = harness.Scope(tenantId);
        var jobs = new IdentityTestContext.RecordingJobService();
        pause.Target = first.Db;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        Task one = Seed(first, jobs).SeedAsync(deadline.Token);
        try
        {
            await pause.Entered.Task.WaitAsync(deadline.Token);
            // The first role has been saved by Identity, but no partial identity is visible.
            Assert.Empty(await setup.Db.Roles.ToListAsync(deadline.Token));
            Assert.Empty(await setup.Db.Users.ToListAsync(deadline.Token));
            Task two = Seed(second, jobs).SeedAsync(deadline.Token);
            await WaitForAdvisoryLockAsync(two, connectionString, deadline.Token);
            pause.Resume.TrySetResult();
            await Task.WhenAll(one, two).WaitAsync(deadline.Token);

            Assert.Single(await setup.Db.Users.ToListAsync());
            Assert.Equal(2, await setup.Db.Roles.CountAsync());
            Assert.Equal(2, await setup.Db.UserRoles.CountAsync());
            var claims = await setup.Db.RoleClaims.ToListAsync();
            Assert.Equal(PermissionCatalog.Admin.Count + PermissionCatalog.Basic.Count, claims.Count);
            Assert.All(claims.GroupBy(claim => (claim.RoleId, claim.ClaimType, claim.ClaimValue)),
                group => Assert.Single(group));
            Assert.Single(jobs.Emails);
        }
        finally { pause.Resume.TrySetResult(); }
    }

    [PostgresMfaFact]
    public async Task FailedOwnerCreationRollsBackRolesAndClaimsAndClearsTrackedStateForRetry()
    {
        using var harness = new MfaHarness(ConnectionString());
        string tenantId = Guid.NewGuid().ToString();
        using MfaScope scope = harness.Scope(tenantId);
        await PrepareAsync(scope, tenantId);
        var jobs = new IdentityTestContext.RecordingJobService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Seed(scope, jobs, email: "").SeedAsync());

        Assert.Empty(scope.Db.ChangeTracker.Entries());
        await AssertNoIdentityAsync(scope);
        Assert.Empty(jobs.Emails);
        await Seed(scope, jobs).SeedAsync();
        Assert.Single(await scope.Db.Users.ToListAsync());
        Assert.Equal(2, await scope.Db.Roles.CountAsync());
        Assert.Single(jobs.Emails);
    }

    [PostgresMfaFact]
    public async Task CancellationAfterAnIdentitySaveRollsBackTheWholeBootstrap()
    {
        using var cancellation = new CancellationTokenSource();
        var cancel = new CancelAfterSave(cancellation);
        using var harness = new MfaHarness(ConnectionString(), interceptor: cancel);
        string tenantId = Guid.NewGuid().ToString();
        using MfaScope scope = harness.Scope(tenantId);
        await PrepareAsync(scope, tenantId);
        cancel.Target = scope.Db;
        var jobs = new IdentityTestContext.RecordingJobService();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Seed(scope, jobs).SeedAsync(cancellation.Token));

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Empty(scope.Db.ChangeTracker.Entries());
        await AssertNoIdentityAsync(scope);
        Assert.Empty(jobs.Emails);
    }

    private static DataSeedService Seed(MfaScope scope, IdentityTestContext.RecordingJobService jobs,
        string email = "owner@example.com")
    {
        ITenantGetterService tenant = scope.Services.GetRequiredService<ITenantGetterService>();
        tenant.Tenant.AdminEmail = email;
        return new DataSeedService(tenant, jobs, scope.Db,
            scope.Services.GetRequiredService<TenantDbContext>(),
            scope.Services.GetRequiredService<RoleManager<ApplicationRole>>(), scope.Users,
            new ConfigurationBuilder().Build(), new HostingEnvironment { EnvironmentName = Environments.Production });
    }

    private static async Task PrepareAsync(MfaScope scope, string tenantId)
    {
        await scope.Db.Database.MigrateAsync();
        TenantDbContext registry = scope.Services.GetRequiredService<TenantDbContext>();
        registry.Tenants.Add(new Tenant { Id = tenantId, Identifier = tenantId, AdminEmail = "owner@example.com" });
        await registry.SaveChangesAsync();
    }

    private static async Task AssertNoIdentityAsync(MfaScope scope)
    {
        Assert.Empty(await scope.Db.Users.ToListAsync());
        Assert.Empty(await scope.Db.Roles.ToListAsync());
        Assert.Empty(await scope.Db.RoleClaims.ToListAsync());
        Assert.Empty(await scope.Db.UserRoles.ToListAsync());
    }

    private static string ConnectionString() => new NpgsqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!)
    {
        ApplicationName = "krafter-bootstrap-" + Guid.NewGuid().ToString("N")
    }.ConnectionString;

    private static async Task WaitForAdvisoryLockAsync(Task operation, string connectionString,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (SELECT FROM pg_stat_activity
            WHERE application_name = @name AND wait_event_type = 'Lock' AND wait_event = 'advisory')
            """, connection);
        command.Parameters.AddWithValue("name", new NpgsqlConnectionStringBuilder(connectionString).ApplicationName!);
        while (!operation.IsCompleted)
        {
            if ((bool)(await command.ExecuteScalarAsync(cancellationToken))!) return;
            await Task.Delay(25, cancellationToken);
        }
        await operation;
        Assert.Fail("Concurrent provisioning completed before the first bootstrap committed.");
    }

    private sealed class PauseAfterSave : SaveChangesInterceptor
    {
        public ApplicationDbContext? Target { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (ReferenceEquals(eventData.Context, Target))
            {
                Target = null;
                Entered.TrySetResult();
                await Resume.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class CancelAfterSave(CancellationTokenSource cancellation) : SaveChangesInterceptor
    {
        public ApplicationDbContext? Target { get; set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (ReferenceEquals(eventData.Context, Target)) cancellation.Cancel();
            return ValueTask.FromResult(result);
        }
    }
}
