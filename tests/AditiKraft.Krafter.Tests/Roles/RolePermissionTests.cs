using AditiKraft.Krafter.Backend.Features.Roles;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Tests.Users;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Tests.Roles;

public sealed class RolePermissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothUpdateOperationsClearEveryPermissionWithoutRemovingOtherClaimTypes(bool permissionsOnly)
    {
        await using var context = new IdentityTestContext();
        var role = await context.AddRoleAsync("Editor");
        context.Db.RoleClaims.AddRange(
            Claim(role.Id, AppClaimTypes.Permission, "Permissions.Users.View"),
            Claim(role.Id, "custom", "custom-value"));
        await context.Db.SaveChangesAsync();
        var service = new RolePermissionService(context.Db, context.Tenant);

        var result = permissionsOnly
            ? await new UpdateRolePermissions.Handler(context.Roles, context.Db, service).UpdatePermissionsAsync(
                new UpdateRolePermissionsRequest { RoleId = role.Id, Permissions = [] }, CancellationToken.None)
            : await new UpdateRole.Handler(context.Roles, context.Db, service).UpdateAsync(role.Id,
                new CreateOrUpdateRoleRequest { Name = role.Name!, Permissions = [] }, CancellationToken.None);

        Assert.False(result.IsError, result.Message);
        context.Db.ChangeTracker.Clear();
        Assert.Equal("custom", (await context.Db.RoleClaims.SingleAsync()).ClaimType);
        Assert.True((await context.Db.RoleClaims.IgnoreQueryFilters()
            .SingleAsync(claim => claim.ClaimType == AppClaimTypes.Permission)).IsDeleted);
    }

    [Fact]
    public async Task NullPermissionsLeaveExistingPermissionsDuringMetadataUpdate()
    {
        await using var context = new IdentityTestContext();
        var role = await context.AddRoleAsync("Editor");
        context.Db.RoleClaims.Add(Claim(role.Id, AppClaimTypes.Permission, "Permissions.Users.View"));
        await context.Db.SaveChangesAsync();
        var handler = new UpdateRole.Handler(context.Roles, context.Db,
            new RolePermissionService(context.Db, context.Tenant));

        var result = await handler.UpdateAsync(role.Id,
            new CreateOrUpdateRoleRequest { Name = role.Name!, Description = "Changed", Permissions = null! },
            CancellationToken.None);

        Assert.False(result.IsError, result.Message);
        context.Db.ChangeTracker.Clear();
        Assert.Equal("Permissions.Users.View", (await context.Db.RoleClaims.SingleAsync()).ClaimValue);
        Assert.Equal("Changed", (await context.Db.Roles.SingleAsync()).Description);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothUpdateOperationsKeepAdminPermissionsProtected(bool permissionsOnly)
    {
        await using var context = new IdentityTestContext();
        var role = await context.AddRoleAsync(RoleConstants.Admin);
        context.Db.RoleClaims.Add(Claim(role.Id, AppClaimTypes.Permission, "Permissions.Users.View"));
        await context.Db.SaveChangesAsync();
        var service = new RolePermissionService(context.Db, context.Tenant);

        var result = permissionsOnly
            ? await new UpdateRolePermissions.Handler(context.Roles, context.Db, service).UpdatePermissionsAsync(
                new UpdateRolePermissionsRequest { RoleId = role.Id, Permissions = [] }, CancellationToken.None)
            : await new UpdateRole.Handler(context.Roles, context.Db, service).UpdateAsync(role.Id,
                new CreateOrUpdateRoleRequest { Name = role.Name!, Permissions = [] }, CancellationToken.None);

        Assert.True(result.IsError);
        context.Db.ChangeTracker.Clear();
        Assert.Equal("Permissions.Users.View", (await context.Db.RoleClaims.SingleAsync()).ClaimValue);
    }

    [Fact]
    public async Task EmptyPermissionsSoftDeleteOnlyCurrentRolesPermissionClaims()
    {
        await using var context = new IdentityTestContext();
        var role = await context.AddRoleAsync("Editor");
        var otherRole = await context.AddRoleAsync("Other");
        context.Db.RoleClaims.AddRange(
            Claim(role.Id, AppClaimTypes.Permission, "Permissions.Users.View"),
            Claim(role.Id, "custom", "custom-value"),
            Claim(otherRole.Id, AppClaimTypes.Permission, "Permissions.Users.View"));
        await context.Db.SaveChangesAsync();
        context.SetTenant("tenant-b");
        context.Db.RoleClaims.Add(Claim(role.Id, AppClaimTypes.Permission, "foreign-permission"));
        await context.Db.SaveChangesAsync();
        context.SetTenant("tenant-a");
        context.Db.ChangeTracker.Clear();
        var service = new RolePermissionService(context.Db, context.Tenant);

        await service.SynchronizeAsync(role.Id, [], CancellationToken.None);
        await context.Db.SaveChangesAsync();

        context.Db.ChangeTracker.Clear();
        var claims = await context.Db.RoleClaims.IgnoreQueryFilters().ToListAsync();
        Assert.True(claims.Single(c => c.RoleId == role.Id && c.TenantId == "tenant-a" &&
                                      c.ClaimType == AppClaimTypes.Permission).IsDeleted);
        Assert.False(claims.Single(c => c.ClaimType == "custom").IsDeleted);
        Assert.False(claims.Single(c => c.RoleId == otherRole.Id).IsDeleted);
        Assert.False(claims.Single(c => c.TenantId == "tenant-b").IsDeleted);
        Assert.Equal("tenant-b", claims.Single(c => c.ClaimValue == "foreign-permission").TenantId);
    }

    [Fact]
    public async Task RepeatedPermissionsRestoreDeletedClaimAndAddNewClaimOnce()
    {
        await using var context = new IdentityTestContext();
        var role = await context.AddRoleAsync("Editor");
        var deleted = Claim(role.Id, AppClaimTypes.Permission, "Permissions.Users.View");
        deleted.IsDeleted = true;
        context.Db.RoleClaims.AddRange(deleted, Claim(role.Id, AppClaimTypes.Permission, "Permissions.Users.Delete"));
        await context.Db.SaveChangesAsync();
        int deletedClaimId = deleted.Id;
        var service = new RolePermissionService(context.Db, context.Tenant);

        await service.SynchronizeAsync(role.Id,
            ["Permissions.Users.View", "Permissions.Users.View", "Permissions.Users.Create", "Permissions.Users.Create"],
            CancellationToken.None);
        await context.Db.SaveChangesAsync();

        context.Db.ChangeTracker.Clear();
        var claims = await context.Db.RoleClaims.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(3, claims.Count);
        var restored = Assert.Single(claims, c => c.ClaimValue == "Permissions.Users.View");
        Assert.Equal(deletedClaimId, restored.Id);
        Assert.False(restored.IsDeleted);
        Assert.False(Assert.Single(claims, c => c.ClaimValue == "Permissions.Users.Create").IsDeleted);
        Assert.True(Assert.Single(claims, c => c.ClaimValue == "Permissions.Users.Delete").IsDeleted);
    }

    private static ApplicationRoleClaim Claim(string roleId, string type, string value)
        => new() { RoleId = roleId, ClaimType = type, ClaimValue = value };
}
