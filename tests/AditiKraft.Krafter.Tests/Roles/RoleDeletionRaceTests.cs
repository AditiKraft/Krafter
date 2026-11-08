using AditiKraft.Krafter.Backend.Features.Roles;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Tests.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Roles;

[Collection("MFA PostgreSQL")]
public sealed class RoleDeletionRaceTests
{
    [Fact]
    public Task InFlightEditCannotRestoreADeletedRoleOrItsPermissions() => VerifyAsync(null);

    [PostgresMfaFact]
    public Task PostgresInFlightEditCannotRestoreADeletedRoleOrItsPermissions() =>
        VerifyAsync(Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!);

    private static async Task VerifyAsync(string? postgres)
    {
        var interceptor = new BeforeSaveInterloper();
        using var harness = new MfaHarness(postgres, interceptor: interceptor);
        string tenantId = Guid.NewGuid().ToString();
        using MfaScope seed = harness.Scope(tenantId);
        if (seed.Db.Database.IsRelational()) await seed.Db.Database.MigrateAsync();
        var user = await seed.AddUserAsync();
        var role = new ApplicationRole("Operator") { Id = Guid.NewGuid().ToString() };
        Assert.True((await seed.Services.GetRequiredService<RoleManager<ApplicationRole>>().CreateAsync(role)).Succeeded);
        seed.Db.UserRoles.Add(new ApplicationUserRole { UserId = user.Id, RoleId = role.Id });
        seed.Db.RoleClaims.Add(new ApplicationRoleClaim
        { RoleId = role.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = "Permissions.Users.View" });
        await seed.Db.SaveChangesAsync();
        bool deletionObserved = false;

        interceptor.BeforeNextSave = async () =>
        {
            using MfaScope deleting = harness.Scope(tenantId);
            var result = await new DeleteRole.Handler(
                deleting.Services.GetRequiredService<RoleManager<ApplicationRole>>(), deleting.Db).DeleteAsync(role.Id);
            Assert.False(result.IsError, result.Message);
            deletionObserved = true;
        };
        using MfaScope editing = harness.Scope(tenantId);
        var handler = new UpdateRole.Handler(editing.Services.GetRequiredService<RoleManager<ApplicationRole>>(), editing.Db,
            new RolePermissionService(editing.Db, editing.Services.GetRequiredService<ITenantGetterService>()));
        var rejected = await handler.UpdateAsync(role.Id,
            new() { Name = "Operator", Description = "Concurrent edit", Permissions = ["Permissions.Users.View"] },
            CancellationToken.None);

        Assert.True(deletionObserved);
        Assert.True(rejected.IsError);
        Assert.DoesNotContain(editing.Db.ChangeTracker.Entries<ApplicationRole>(), entry => entry.Entity.Id == role.Id);
        await editing.Db.SaveChangesAsync();
        using MfaScope fresh = harness.Scope(tenantId);
        Assert.True((await fresh.Db.Roles.IgnoreQueryFilters().SingleAsync(item => item.Id == role.Id)).IsDeleted);
        Assert.True((await fresh.Db.RoleClaims.IgnoreQueryFilters().SingleAsync(claim => claim.RoleId == role.Id)).IsDeleted);
        Assert.False((await fresh.Services.GetRequiredService<IUserService>()
            .HasPermissionAsync(user.Id, "Permissions.Users.View")).Data);
    }

    private sealed class BeforeSaveInterloper : SaveChangesInterceptor
    {
        public Func<Task>? BeforeNextSave;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Func<Task>? action = BeforeNextSave;
            BeforeNextSave = null;
            if (action is not null) await action();
            return result;
        }
    }
}
