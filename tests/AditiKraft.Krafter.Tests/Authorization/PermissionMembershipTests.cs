using System.Data.Common;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Roles;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Tests.Auth;
using AditiKraft.Krafter.Tests.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Authorization;

[Collection("MFA PostgreSQL")]
public sealed class PermissionMembershipTests
{
    private const string OwnPermission = "Permissions.Roles.Update";
    private const string UnassignedPermission = "Permissions.UserRoles.Update";

    [PostgresMfaFact]
    public Task ConcurrentRoleRenamesCannotTransferAuthorityToThePermissionService() =>
        VerifyRoleRenameAsync(useEndpointHandler: false);

    [PostgresMfaFact]
    public Task ConcurrentRoleRenamesCannotTransferAuthorityToThePermissionEndpoint() =>
        VerifyRoleRenameAsync(useEndpointHandler: true);

    private static async Task VerifyRoleRenameAsync(bool useEndpointHandler)
    {
        string actorId = Guid.NewGuid().ToString();
        var interleaver = new BeforePermissionLookup();
        await using var provider = DomainTestServices.Provider(Guid.NewGuid().ToString(), actorId,
            Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!, identityInterceptor: interleaver);
        await DomainTestServices.MigrateAsync(provider);
        string assignedId;
        string unassignedId;
        await using (var seed = provider.CreateAsyncScope())
        {
            var services = seed.ServiceProvider;
            await DomainTestServices.AddUserAsync(services, "actor@example.com", actorId);
            assignedId = (await DomainTestServices.AddRoleAsync(services, "Low")).Id;
            unassignedId = (await DomainTestServices.AddRoleAsync(services, "High")).Id;
            var db = services.GetRequiredService<ApplicationDbContext>();
            db.UserRoles.Add(new ApplicationUserRole { UserId = actorId, RoleId = assignedId });
            db.RoleClaims.AddRange(
                Claim(assignedId, OwnPermission),
                Claim(assignedId, "Permissions.Users.Update"),
                Claim(unassignedId, UnassignedPermission));
            await db.SaveChangesAsync();
        }

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var request = provider.CreateAsyncScope();
        IUserService permissions = request.ServiceProvider.GetRequiredService<IUserService>();
        Assert.False((await permissions.HasPermissionAsync(actorId, UnassignedPermission, deadline.Token)).Data);
        interleaver.Target = request.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        interleaver.BeforeLookup = async () =>
        {
            // These are ordinary metadata-only updates. Neither operation requires claim or
            // membership authority, and the names remain unique at every committed step.
            await RenameAsync(assignedId, "Temp");
            await RenameAsync(unassignedId, "Low");
        };

        Response<List<string>> result = useEndpointHandler
            ? await ActivatorUtilities.CreateInstance<GetUserPermissions.Handler>(request.ServiceProvider)
                .GetPermissionsAsync(actorId, deadline.Token)
            : await permissions.GetPermissionsAsync(actorId, deadline.Token);

        Assert.True(interleaver.Interleaved);
        Assert.False(result.IsError, result.Message);
        Assert.Contains(OwnPermission, result.Data!);
        Assert.DoesNotContain(UnassignedPermission, result.Data!);
        await using var verify = provider.CreateAsyncScope();
        var verifiedDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(assignedId, (await verifiedDb.UserRoles.SingleAsync(link => link.UserId == actorId)).RoleId);
        Assert.Equal("Temp", (await verifiedDb.Roles.SingleAsync(role => role.Id == assignedId)).Name);
        Assert.Equal("Low", (await verifiedDb.Roles.SingleAsync(role => role.Id == unassignedId)).Name);
        Assert.False((await verify.ServiceProvider.GetRequiredService<IUserService>()
            .HasPermissionAsync(actorId, UnassignedPermission, deadline.Token)).Data);

        async Task RenameAsync(string roleId, string name)
        {
            await using var rename = provider.CreateAsyncScope();
            var services = rename.ServiceProvider;
            var handler = new UpdateRole.Handler(services.GetRequiredService<RoleManager<ApplicationRole>>(),
                services.GetRequiredService<ApplicationDbContext>(), services.GetRequiredService<RolePermissionService>(),
                services.GetRequiredService<ITenantGetterService>());
            Response renamed = await handler.UpdateAsync(roleId,
                new() { Name = name, Description = "Metadata only", Permissions = null }, deadline.Token);
            Assert.False(renamed.IsError, renamed.Message);
        }
    }

    [Theory]
    [InlineData("none")]
    [InlineData("membership")]
    [InlineData("role")]
    [InlineData("claim")]
    [InlineData("user")]
    [InlineData("tenant")]
    public async Task PermissionReadsKeepMembershipDeletionAndTenantFilters(string change)
    {
        string tenantId = Guid.NewGuid().ToString();
        await using var provider = DomainTestServices.Provider(tenantId);
        string userId;
        await using (var seed = provider.CreateAsyncScope())
        {
            var services = seed.ServiceProvider;
            userId = (await DomainTestServices.AddUserAsync(services, "member@example.com")).Id;
            string roleId = (await DomainTestServices.AddRoleAsync(services, "Members")).Id;
            string unassignedId = (await DomainTestServices.AddRoleAsync(services, "Unassigned")).Id;
            var db = services.GetRequiredService<ApplicationDbContext>();
            db.UserRoles.Add(new ApplicationUserRole { UserId = userId, RoleId = roleId });
            db.RoleClaims.AddRange(Claim(roleId, OwnPermission), Claim(roleId, OwnPermission),
                Claim(unassignedId, UnassignedPermission),
                new ApplicationRoleClaim { RoleId = roleId, ClaimType = "custom", ClaimValue = UnassignedPermission });
            await db.SaveChangesAsync();
        }
        await using (var mutation = provider.CreateAsyncScope())
        {
            var db = mutation.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            switch (change)
            {
                case "membership": (await db.UserRoles.SingleAsync()).IsDeleted = true; break;
                case "role": (await db.Roles.SingleAsync(role => role.Name == "Members")).IsDeleted = true; break;
                case "claim":
                    foreach (var claim in await db.RoleClaims.Where(claim => claim.ClaimValue == OwnPermission).ToListAsync())
                        claim.IsDeleted = true;
                    break;
                case "user": (await db.Users.SingleAsync()).IsDeleted = true; break;
            }
            await db.SaveChangesAsync();
        }

        await using var request = provider.CreateAsyncScope();
        if (change == "tenant")
        {
            DomainTestServices.SelectTenant(request.ServiceProvider, Guid.NewGuid().ToString());
            // The same role name in another tenant must not provide permissions for this user ID.
            var foreignUser = await DomainTestServices.AddUserAsync(request.ServiceProvider, "member@example.com");
            var foreignRole = await DomainTestServices.AddRoleAsync(request.ServiceProvider, "Members");
            var foreignDb = request.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreignDb.UserRoles.Add(new ApplicationUserRole { UserId = foreignUser.Id, RoleId = foreignRole.Id });
            foreignDb.RoleClaims.Add(Claim(foreignRole.Id, UnassignedPermission));
            await foreignDb.SaveChangesAsync();
        }
        IUserService service = request.ServiceProvider.GetRequiredService<IUserService>();
        Response<List<string>> actual = await service.GetPermissionsAsync(userId, CancellationToken.None);
        Response<List<string>> endpoint = await ActivatorUtilities.CreateInstance<GetUserPermissions.Handler>(request.ServiceProvider)
            .GetPermissionsAsync(userId, CancellationToken.None);
        Assert.Equal(change is "user" or "tenant" ? 404 : 200, actual.StatusCode);
        Assert.Equal(actual.StatusCode, endpoint.StatusCode);
        Assert.Equal(actual.Data, endpoint.Data);
        if (!actual.IsError)
            Assert.Equal(change == "none" ? new[] { OwnPermission } : [], actual.Data);
        Assert.Equal(change == "none", (await service.HasPermissionAsync(userId, OwnPermission)).Data);
        Assert.False((await service.HasPermissionAsync(userId, UnassignedPermission)).Data);
    }

    private static ApplicationRoleClaim Claim(string roleId, string value) =>
        new() { RoleId = roleId, ClaimType = AppClaimTypes.Permission, ClaimValue = value };

    private sealed class BeforePermissionLookup : DbCommandInterceptor
    {
        public DbContext? Target { get; set; }
        public Func<Task>? BeforeLookup { get; set; }
        public bool Interleaved { get; private set; }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            // With the old lookup this runs after Identity returned membership names, before
            // the separate role query. With the corrected join it runs before the one snapshot
            // that resolves membership IDs and claims. In both cases the same renames commit.
            bool permissionLookup = command.CommandText.Contains("\"ApplicationRole\"", StringComparison.Ordinal) &&
                (!command.CommandText.Contains("\"ApplicationUserRole\"", StringComparison.Ordinal) ||
                 command.CommandText.Contains("\"ApplicationRoleClaim\"", StringComparison.Ordinal));
            if (ReferenceEquals(eventData.Context, Target) && permissionLookup && BeforeLookup is { } action)
            {
                BeforeLookup = null;
                await action();
                Interleaved = true;
            }
            return result;
        }
    }
}
