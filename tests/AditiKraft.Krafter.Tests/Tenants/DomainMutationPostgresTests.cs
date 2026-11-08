using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Roles;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using AditiKraft.Krafter.Tests.Auth;
using AditiKraft.Krafter.Tests.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Tenants;

[Collection("MFA PostgreSQL")]
public sealed class DomainMutationPostgresTests
{
    private static string Connection => Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!;
    private static string Identifier() => "d" + Guid.NewGuid().ToString("N")[..9];

    [PostgresMfaFact]
    public async Task TenantCreationRejectsUnsupportedOwnerIdentifiersBeforeRegistryWrites()
    {
        await using var provider = DomainTestServices.Provider("root", postgres: Connection);
        await DomainTestServices.MigrateAsync(provider);
        foreach (string email in new[] { "o'brien@example.com", new string('a', 250) + "@example.com" })
        {
            var request = TenantRequest(Identifier(), email);
            await using (var scope = provider.CreateAsyncScope())
                Assert.Equal(400, (await Create(scope.ServiceProvider).CreateAsync(request, CancellationToken.None)).StatusCode);
            await AssertAbsentAsync(provider, request.Id, request.Identifier);
            request.AdminEmail = "valid.owner@example.com";
            await using var retry = provider.CreateAsyncScope();
            Assert.False((await Create(retry.ServiceProvider).CreateAsync(request, CancellationToken.None)).IsError);
        }
    }

    [PostgresMfaFact]
    public async Task TenantProvisioningFailureRollsBackRegistryRolesClaimsAndOwnerThenRetrySucceeds()
    {
        var fault = new MutationInterceptor();
        string actor = Guid.NewGuid().ToString();
        await using var provider = DomainTestServices.Provider("root", actor, Connection, identityInterceptor: fault);
        await DomainTestServices.MigrateAsync(provider);
        await AddActorAsync(provider, actor);
        var request = TenantRequest(Identifier());
        fault.AfterSave = _ => throw new InvalidOperationException("Injected identity store failure.");
        await using (var scope = provider.CreateAsyncScope())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Create(scope.ServiceProvider).CreateAsync(request, CancellationToken.None));
            Assert.Empty(scope.ServiceProvider.GetRequiredService<TenantDbContext>().ChangeTracker.Entries());
            await scope.ServiceProvider.GetRequiredService<TenantDbContext>().SaveChangesAsync();
        }
        await AssertAbsentAsync(provider, request.Id, request.Identifier);
        Assert.Empty(provider.GetRequiredService<DomainJobs>().Emails);

        provider.GetRequiredService<DomainJobs>().BeforeEnqueue = async () =>
        {
            await using var fresh = provider.CreateAsyncScope();
            DomainTestServices.SelectTenant(fresh.ServiceProvider, request.Id!);
            var db = fresh.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await fresh.ServiceProvider.GetRequiredService<TenantDbContext>().Tenants.AnyAsync(t => t.Id == request.Id));
            ApplicationUser owner = await db.Users.SingleAsync();
            Assert.True(owner.IsOwner);
            Assert.Equal(actor, owner.CreatedById);
            Assert.Equal(2, await db.UserRoles.CountAsync());
        };
        await using var retry = provider.CreateAsyncScope();
        Assert.False((await Create(retry.ServiceProvider).CreateAsync(request, CancellationToken.None)).IsError);
        Assert.Single(provider.GetRequiredService<DomainJobs>().Emails);
    }

    [PostgresMfaFact]
    public async Task TenantCancellationBeforeCommitRollsBackEveryRequiredWrite()
    {
        foreach (string boundary in new[] { "registry", "identity", "memberships" })
        {
            using var cancellation = new CancellationTokenSource();
            var fault = new MutationInterceptor();
            await using var provider = DomainTestServices.Provider("root", postgres: Connection,
                identityInterceptor: fault, registryInterceptor: fault);
            await DomainTestServices.MigrateAsync(provider);
            var request = TenantRequest(Identifier());
            fault.AfterSavePredicate = db => boundary switch
            {
                "registry" => db is TenantDbContext,
                "identity" => db is ApplicationDbContext,
                _ => db is ApplicationDbContext identity && identity.UserRoles.Local.Count > 0
            };
            fault.AfterSave = _ => { cancellation.Cancel(); return Task.CompletedTask; };
            await using (var scope = provider.CreateAsyncScope())
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    Create(scope.ServiceProvider).CreateAsync(request, cancellation.Token));
            Assert.True(cancellation.IsCancellationRequested);
            await AssertAbsentAsync(provider, request.Id, request.Identifier);
            Assert.Empty(provider.GetRequiredService<DomainJobs>().Emails);
        }
    }

    [PostgresMfaFact]
    public async Task LateTenantIdentifierConflictRollsBackOwnerEmailAndRetryTargetsSameOwner()
    {
        var fault = new MutationInterceptor();
        string actor = Guid.NewGuid().ToString();
        await using var provider = DomainTestServices.Provider("root", actor, Connection, registryInterceptor: fault);
        await DomainTestServices.MigrateAsync(provider);
        await AddActorAsync(provider, actor);
        var original = TenantRequest(Identifier());
        await using (var seed = provider.CreateAsyncScope())
            Assert.False((await Create(seed.ServiceProvider).CreateAsync(original, CancellationToken.None)).IsError);
        string tenantId = original.Id!;
        string desiredIdentifier = Identifier();
        var update = TenantRequest(desiredIdentifier, "changed@example.com");
        update.Name = "Changed tenant";
        fault.BeforeSave = async _ =>
        {
            // The owner's Identity save already succeeded in the still-uncommitted transaction.
            // A concurrent registry writer now wins the identifier checked earlier by UpdateTenant.
            await using var competitor = provider.CreateAsyncScope();
            var registry = competitor.ServiceProvider.GetRequiredService<TenantDbContext>();
            registry.Tenants.Add(new Tenant
            { Id = Guid.NewGuid().ToString(), Identifier = desiredIdentifier, AdminEmail = "competitor@example.com" });
            await registry.SaveChangesAsync();
        };
        string ownerId;
        string? passwordHash;
        await using (var before = provider.CreateAsyncScope())
        {
            DomainTestServices.SelectTenant(before.ServiceProvider, tenantId);
            ApplicationUser owner = await before.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync();
            (ownerId, passwordHash) = (owner.Id, owner.PasswordHash);
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            Assert.Equal(409, (await Update(scope.ServiceProvider).UpdateAsync(tenantId, update, CancellationToken.None)).StatusCode);
            Assert.Empty(scope.ServiceProvider.GetRequiredService<TenantDbContext>().ChangeTracker.Entries());
            await scope.ServiceProvider.GetRequiredService<TenantDbContext>().SaveChangesAsync();
        }
        await AssertEmailsAsync(provider, tenantId, original.AdminEmail);

        update.Identifier = original.Identifier;
        update.AdminEmail = "final@example.com";
        await using (var retry = provider.CreateAsyncScope())
            Assert.False((await Update(retry.ServiceProvider).UpdateAsync(tenantId, update, CancellationToken.None)).IsError);
        await AssertEmailsAsync(provider, tenantId, update.AdminEmail);
        await using var verify = provider.CreateAsyncScope();
        DomainTestServices.SelectTenant(verify.ServiceProvider, tenantId);
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser final = await db.Users.SingleAsync();
        Assert.Equal(ownerId, final.Id);
        Assert.Equal(passwordHash, final.PasswordHash);
        Assert.Equal("Admin", final.FirstName);
        Assert.Equal(2, await db.UserRoles.CountAsync());
        Assert.Equal(actor, final.UpdatedById);
    }

    [PostgresMfaFact]
    public async Task CancelingAfterOwnerEmailSaveRollsBackTheRegistryAndIdentityPair()
    {
        using var cancellation = new CancellationTokenSource();
        var fault = new MutationInterceptor();
        await using var provider = DomainTestServices.Provider("root", postgres: Connection, identityInterceptor: fault);
        await DomainTestServices.MigrateAsync(provider);
        var request = TenantRequest(Identifier());
        await using (var seed = provider.CreateAsyncScope())
            Assert.False((await Create(seed.ServiceProvider).CreateAsync(request, CancellationToken.None)).IsError);
        string originalEmail = request.AdminEmail;
        request.AdminEmail = "changed@example.com";
        fault.AfterSave = _ => { cancellation.Cancel(); return Task.CompletedTask; };
        await using (var scope = provider.CreateAsyncScope())
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Update(scope.ServiceProvider)
                .UpdateAsync(request.Id!, request, cancellation.Token));
        await AssertEmailsAsync(provider, request.Id!, originalEmail);
    }

    [PostgresMfaFact]
    public async Task UserDrivenOwnerEmailChangeRollsBackProfileAndIdentityWhenRegistrySaveFails()
    {
        string tenantId = Guid.NewGuid().ToString();
        string actorId = Guid.NewGuid().ToString();
        var fault = new MutationInterceptor();
        await using var provider = DomainTestServices.Provider(tenantId, actorId, Connection, registryInterceptor: fault);
        await DomainTestServices.MigrateAsync(provider);
        await using (var seed = provider.CreateAsyncScope())
        {
            await DomainTestServices.AddUserAsync(seed.ServiceProvider, "owner@example.com", actorId, owner: true);
            var registry = seed.ServiceProvider.GetRequiredService<TenantDbContext>();
            registry.Tenants.Add(new Tenant
            { Id = tenantId, Identifier = Identifier(), AdminEmail = "owner@example.com" });
            await registry.SaveChangesAsync();
        }
        fault.BeforeSave = _ => throw new InvalidOperationException("Injected registry failure.");
        await using (var scope = provider.CreateAsyncScope())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                scope.ServiceProvider.GetRequiredService<IUserMutationService>().UpdateAsync(actorId,
                    new CreateUserRequest { FirstName = "Changed", LastName = "Profile", Email = "changed@example.com" }, CancellationToken.None));
            Assert.Empty(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ChangeTracker.Entries());
            Assert.Empty(scope.ServiceProvider.GetRequiredService<TenantDbContext>().ChangeTracker.Entries());
            // The participant has its own connection again and cannot reuse the rolled-back transaction.
            await scope.ServiceProvider.GetRequiredService<TenantDbContext>().SaveChangesAsync();
        }
        await AssertEmailsAsync(provider, tenantId, "owner@example.com");
        await using var verify = provider.CreateAsyncScope();
        Assert.Equal("First", (await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync()).FirstName);
    }

    [PostgresMfaFact]
    public async Task MissingOrConcurrentlyDeletedOwnerRegistryReturnsConflictAndRollsBackIdentity()
    {
        foreach (bool deleteAfterIdentitySave in new[] { false, true })
        {
            string tenantId = Guid.NewGuid().ToString();
            string ownerId = Guid.NewGuid().ToString();
            var fault = new MutationInterceptor();
            await using var provider = DomainTestServices.Provider(tenantId, ownerId, Connection, identityInterceptor: fault);
            await DomainTestServices.MigrateAsync(provider);
            string? originalStamp;
            await using (var seed = provider.CreateAsyncScope())
            {
                ApplicationUser owner = await DomainTestServices.AddUserAsync(seed.ServiceProvider,
                    "owner@example.com", ownerId, owner: true);
                originalStamp = owner.ConcurrencyStamp;
                if (deleteAfterIdentitySave)
                {
                    var registry = seed.ServiceProvider.GetRequiredService<TenantDbContext>();
                    registry.Tenants.Add(new Tenant
                    { Id = tenantId, Identifier = Identifier(), AdminEmail = "owner@example.com" });
                    await registry.SaveChangesAsync();
                }
            }
            if (deleteAfterIdentitySave)
                fault.AfterSave = async _ =>
                {
                    // Another request removes the registry after Identity saves but before synchronization.
                    await using var deletion = provider.CreateAsyncScope();
                    var registry = deletion.ServiceProvider.GetRequiredService<TenantDbContext>();
                    (await registry.Tenants.SingleAsync(tenant => tenant.Id == tenantId)).IsDeleted = true;
                    await registry.SaveChangesAsync();
                };

            await using (var scope = provider.CreateAsyncScope())
            {
                Response result = await scope.ServiceProvider.GetRequiredService<IUserMutationService>()
                    .UpdateAsync(ownerId, new CreateUserRequest
                    { FirstName = "Changed", LastName = "Profile", Email = "changed@example.com", UpdateTenantEmail = true },
                        CancellationToken.None);
                Assert.Equal(409, result.StatusCode);
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Empty(db.ChangeTracker.Entries());
                Assert.Empty(scope.ServiceProvider.GetRequiredService<TenantDbContext>().ChangeTracker.Entries());
                Assert.Equal(0, await db.SaveChangesAsync());
            }
            await using var verify = provider.CreateAsyncScope();
            ApplicationUser saved = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync();
            Assert.Equal("owner@example.com", saved.Email);
            Assert.Equal("owner@example.com", saved.UserName);
            Assert.Equal("First", saved.FirstName);
            Assert.Equal("Last", saved.LastName);
            Assert.Equal(originalStamp, saved.ConcurrencyStamp);
            Tenant? retained = await verify.ServiceProvider.GetRequiredService<TenantDbContext>().Tenants.IgnoreQueryFilters()
                .SingleOrDefaultAsync(tenant => tenant.Id == tenantId);
            if (deleteAfterIdentitySave)
            {
                Assert.NotNull(retained);
                Assert.True(retained.IsDeleted);
                Assert.Equal("owner@example.com", retained.AdminEmail);
            }
            else Assert.Null(retained);
            Assert.Empty(provider.GetRequiredService<DomainJobs>().Emails);
        }
    }

    [PostgresMfaFact]
    public async Task UserCreationRollsBackOnCancellationOrMembershipFailureAndOnlyQueuesCommittedAccounts()
    {
        foreach (bool cancel in new[] { true, false })
        {
            string tenantId = Guid.NewGuid().ToString();
            using var cancellation = new CancellationTokenSource();
            var fault = new MutationInterceptor();
            await using var provider = DomainTestServices.Provider(tenantId, postgres: Connection, identityInterceptor: fault);
            await DomainTestServices.MigrateAsync(provider);
            await using (var seed = provider.CreateAsyncScope())
                await DomainTestServices.AddRoleAsync(seed.ServiceProvider, RoleConstants.Basic);
            if (cancel)
                fault.AfterSave = _ => { cancellation.Cancel(); return Task.CompletedTask; };
            else
            {
                fault.BeforeSavePredicate = db => db.ChangeTracker.Entries<ApplicationUserRole>().Any(e => e.State == EntityState.Added);
                fault.BeforeSave = _ => throw new InvalidOperationException("Injected membership failure.");
            }
            var request = new CreateUserRequest { FirstName = "New", LastName = "User", Email = "new@example.com" };
            await using (var scope = provider.CreateAsyncScope())
            {
                Task<Response> operation = scope.ServiceProvider.GetRequiredService<IUserMutationService>().CreateAsync(request, cancellation.Token);
                if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
                else await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
                Assert.Empty(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ChangeTracker.Entries());
            }
            await using (var verify = provider.CreateAsyncScope())
            {
                var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Empty(await db.Users.ToListAsync());
                Assert.Empty(await db.UserRoles.ToListAsync());
            }
            Assert.Empty(provider.GetRequiredService<DomainJobs>().Emails);
            provider.GetRequiredService<DomainJobs>().BeforeEnqueue = async () =>
            {
                await using var committed = provider.CreateAsyncScope();
                var db = committed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Single(await db.Users.ToListAsync());
                Assert.Single(await db.UserRoles.ToListAsync());
            };
            await using var retry = provider.CreateAsyncScope();
            Assert.False((await retry.ServiceProvider.GetRequiredService<IUserMutationService>().CreateAsync(request, CancellationToken.None)).IsError);
            Assert.Single(provider.GetRequiredService<DomainJobs>().Emails);
        }
    }

    [PostgresMfaFact]
    public async Task DeletedIdentityIdentifiersRemainReservedIgnoringCaseAndAreIndependentAcrossTenants()
    {
        string tenantId = Guid.NewGuid().ToString();
        await using var provider = DomainTestServices.Provider(tenantId, postgres: Connection);
        await DomainTestServices.MigrateAsync(provider);
        await using (var seed = provider.CreateAsyncScope())
        {
            var services = seed.ServiceProvider;
            await DomainTestServices.AddRoleAsync(services, RoleConstants.Basic);
            var role = await DomainTestServices.AddRoleAsync(services, "Retained");
            var user = await DomainTestServices.AddUserAsync(services, "retained@example.com");
            var db = services.GetRequiredService<ApplicationDbContext>();
            Assert.False((await new DeleteRole.Handler(services.GetRequiredService<RoleManager<ApplicationRole>>(), db).DeleteAsync(role.Id)).IsError);
            Assert.False((await new DeleteUser.Handler(services.GetRequiredService<UserManager<ApplicationUser>>(), db).DeleteAsync(user.Id)).IsError);
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            Assert.Equal(409, (await CreateRoleHandler(scope.ServiceProvider).CreateAsync(
                new() { Name = "RETAINED" }, CancellationToken.None)).StatusCode);
            Assert.Equal(409, (await scope.ServiceProvider.GetRequiredService<IUserMutationService>().CreateAsync(
                new() { FirstName = "New", LastName = "User", Email = "RETAINED@EXAMPLE.COM" }, CancellationToken.None)).StatusCode);
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SaveChangesAsync());
        }
        Assert.Empty(provider.GetRequiredService<DomainJobs>().Emails);
        await using var other = provider.CreateAsyncScope();
        DomainTestServices.SelectTenant(other.ServiceProvider, Guid.NewGuid().ToString());
        await DomainTestServices.AddRoleAsync(other.ServiceProvider, RoleConstants.Basic);
        Assert.False((await CreateRoleHandler(other.ServiceProvider).CreateAsync(new() { Name = "Retained" }, CancellationToken.None)).IsError);
        Assert.False((await other.ServiceProvider.GetRequiredService<IUserMutationService>().CreateAsync(
            new() { FirstName = "New", LastName = "User", Email = "retained@example.com" }, CancellationToken.None)).IsError);
    }

    [PostgresMfaFact]
    public async Task ConcurrentIdentityCreatesReturnOneSuccessAndOneControlledConflict()
    {
        foreach (bool user in new[] { false, true })
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var barrier = new ConcurrentCreateBarrier(user, deadline.Token);
            string tenantId = Guid.NewGuid().ToString();
            await using var provider = DomainTestServices.Provider(tenantId, postgres: Connection, identityInterceptor: barrier);
            await DomainTestServices.MigrateAsync(provider);
            if (user)
            {
                await using var seed = provider.CreateAsyncScope();
                await DomainTestServices.AddRoleAsync(seed.ServiceProvider, RoleConstants.Basic);
            }
            await using var first = provider.CreateAsyncScope();
            await using var second = provider.CreateAsyncScope();
            Task<Response> Write(IServiceProvider services) => user
                ? services.GetRequiredService<IUserMutationService>().CreateAsync(
                    new() { FirstName = "New", LastName = "User", Email = "concurrent@example.com" }, deadline.Token)
                : CreateRoleHandler(services).CreateAsync(new() { Name = "Concurrent" }, deadline.Token);
            Response[] results = await Task.WhenAll(Write(first.ServiceProvider), Write(second.ServiceProvider)).WaitAsync(deadline.Token);
            Assert.Equal(new[] { 200, 409 }, results.Select(result => result.StatusCode).Order());
            await using var verify = provider.CreateAsyncScope();
            var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (user)
            {
                Assert.Single(await db.Users.ToListAsync());
                Assert.Single(await db.UserRoles.ToListAsync());
                Assert.Single(provider.GetRequiredService<DomainJobs>().Emails);
            }
            else Assert.Single(await db.Roles.ToListAsync());
        }
    }

    [PostgresMfaFact]
    public async Task PermissionRevokedAfterRoleMetadataSaveRollsBackCombinedMutation()
    {
        string tenantId = Guid.NewGuid().ToString();
        string actorId = Guid.NewGuid().ToString();
        var fault = new MutationInterceptor();
        await using var provider = DomainTestServices.Provider(tenantId, actorId, Connection, identityInterceptor: fault);
        await DomainTestServices.MigrateAsync(provider);
        string targetId;
        await using (var seed = provider.CreateAsyncScope())
        {
            var actor = await DomainTestServices.AddUserAsync(seed.ServiceProvider, "actor@example.com", actorId);
            var manager = await DomainTestServices.AddRoleAsync(seed.ServiceProvider, "Manager");
            targetId = (await DomainTestServices.AddRoleAsync(seed.ServiceProvider, "Target")).Id;
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.UserRoles.Add(new ApplicationUserRole { UserId = actor.Id, RoleId = manager.Id });
            db.RoleClaims.Add(new ApplicationRoleClaim
            { RoleId = manager.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = "Permissions.RoleClaims.Update" });
            await db.SaveChangesAsync();
        }
        fault.AfterSave = async _ =>
        {
            await using var revoke = provider.CreateAsyncScope();
            var db = revoke.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.RoleClaims.SingleAsync()).IsDeleted = true;
            await db.SaveChangesAsync();
        };
        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var handler = new UpdateRole.Handler(services.GetRequiredService<RoleManager<ApplicationRole>>(),
                services.GetRequiredService<ApplicationDbContext>(), services.GetRequiredService<RolePermissionService>(),
                services.GetRequiredService<ITenantGetterService>());
            Assert.Equal(403, (await handler.UpdateAsync(targetId,
                new() { Name = "Changed", Permissions = ["Permissions.Users.Delete"] }, CancellationToken.None)).StatusCode);
            Assert.Equal(0, await services.GetRequiredService<ApplicationDbContext>().SaveChangesAsync());
        }
        await using var verify = provider.CreateAsyncScope();
        var final = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Target", (await final.Roles.SingleAsync(role => role.Id == targetId)).Name);
        Assert.Empty(await final.RoleClaims.ToListAsync());
    }

    private static CreateTenant.Handler Create(IServiceProvider services) => new(
        services.GetRequiredService<TenantDbContext>(), services, services.GetRequiredService<ICurrentUser>(), DomainTestServices.Urls);

    private static UpdateTenant.Handler Update(IServiceProvider services) => new(
        services.GetRequiredService<TenantDbContext>(), services, DomainTestServices.Urls, services.GetRequiredService<ICurrentUser>());

    private static CreateRole.Handler CreateRoleHandler(IServiceProvider services) => new(
        services.GetRequiredService<RoleManager<ApplicationRole>>(), services.GetRequiredService<ApplicationDbContext>(),
        services.GetRequiredService<RolePermissionService>(), services.GetRequiredService<ITenantGetterService>());

    private static CreateOrUpdateTenantRequest TenantRequest(string identifier, string email = "owner@example.com") => new()
    { Identifier = identifier, Name = "Tenant", AdminEmail = email, IsActive = true, ValidUpto = DateTime.UtcNow.AddDays(30) };

    private static async Task AssertAbsentAsync(IServiceProvider provider, string? id, string identifier)
    {
        await using var verify = provider.CreateAsyncScope();
        Assert.False(await verify.ServiceProvider.GetRequiredService<TenantDbContext>().Tenants.IgnoreQueryFilters()
            .AnyAsync(tenant => tenant.Identifier == identifier));
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Users.IgnoreQueryFilters().AnyAsync(user => user.TenantId == id));
        Assert.False(await db.Roles.IgnoreQueryFilters().AnyAsync(role => role.TenantId == id));
        Assert.False(await db.RoleClaims.IgnoreQueryFilters().AnyAsync(claim => claim.TenantId == id));
        Assert.False(await db.UserRoles.IgnoreQueryFilters().AnyAsync(link => link.TenantId == id));
    }

    private static async Task AddActorAsync(IServiceProvider provider, string actorId)
    {
        await using var scope = provider.CreateAsyncScope();
        await DomainTestServices.AddUserAsync(scope.ServiceProvider, $"actor-{actorId}@example.com", actorId);
    }

    private static async Task AssertEmailsAsync(IServiceProvider provider, string tenantId, string email)
    {
        await using var verify = provider.CreateAsyncScope();
        DomainTestServices.SelectTenant(verify.ServiceProvider, tenantId);
        Assert.Equal(email, (await verify.ServiceProvider.GetRequiredService<TenantDbContext>().Tenants
            .SingleAsync(tenant => tenant.Id == tenantId)).AdminEmail);
        Assert.Equal(email, (await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync()).Email);
    }

    private sealed class ConcurrentCreateBarrier(bool user, CancellationToken deadline) : SaveChangesInterceptor
    {
        private int _arrived;
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            bool target = user
                ? eventData.Context!.ChangeTracker.Entries<ApplicationUser>().Any(entry => entry.State == EntityState.Added && entry.Entity.Email == "concurrent@example.com")
                : eventData.Context!.ChangeTracker.Entries<ApplicationRole>().Any(entry => entry.State == EntityState.Added && entry.Entity.Name == "Concurrent");
            if (target)
            {
                if (Interlocked.Increment(ref _arrived) == 2) _both.TrySetResult();
                await _both.Task.WaitAsync(deadline);
            }
            return result;
        }
    }

    private sealed class MutationInterceptor : SaveChangesInterceptor
    {
        public Func<DbContext, Task>? BeforeSave { get; set; }
        public Func<DbContext, Task>? AfterSave { get; set; }
        public Func<DbContext, bool> BeforeSavePredicate { get; set; } = _ => true;
        public Func<DbContext, bool> AfterSavePredicate { get; set; } = _ => true;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (BeforeSave is { } action && BeforeSavePredicate(eventData.Context!))
            {
                BeforeSave = null;
                await action(eventData.Context!);
            }
            return result;
        }

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (AfterSave is { } action && AfterSavePredicate(eventData.Context!))
            {
                AfterSave = null;
                await action(eventData.Context!);
            }
            return result;
        }
    }
}
