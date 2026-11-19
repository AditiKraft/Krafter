using AditiKraft.Krafter.Backend.Features.Roles;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
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
        await context.AuthorizeRoleClaimsAsync();
        var service = context.RolePermissions;

        var result = permissionsOnly
            ? await new UpdateRolePermissions.Handler(context.Roles, context.Db, service).UpdatePermissionsAsync(
                new UpdateRolePermissionsRequest { RoleId = role.Id, Permissions = [] }, CancellationToken.None)
            : await new UpdateRole.Handler(context.Roles, context.Db, service, context.Tenant).UpdateAsync(role.Id,
                new CreateOrUpdateRoleRequest { Name = role.Name!, Permissions = [] }, CancellationToken.None);

        Assert.False(result.IsError, result.Message);
        context.Db.ChangeTracker.Clear();
        Assert.Equal("custom", (await context.Db.RoleClaims.Where(claim => claim.RoleId != context.ActorRoleId).SingleAsync()).ClaimType);
        Assert.True((await context.Db.RoleClaims.IgnoreQueryFilters().Where(claim => claim.RoleId != context.ActorRoleId)
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
            context.RolePermissions, context.Tenant);

        var result = await handler.UpdateAsync(role.Id,
            new CreateOrUpdateRoleRequest { Name = role.Name!, Description = "Changed", Permissions = null! },
            CancellationToken.None);

        Assert.False(result.IsError, result.Message);
        context.Db.ChangeTracker.Clear();
        Assert.Equal("Permissions.Users.View", (await context.Db.RoleClaims.Where(claim => claim.RoleId != context.ActorRoleId).SingleAsync()).ClaimValue);
        Assert.Equal("Changed", (await context.Db.Roles.Where(role => role.Id != context.ActorRoleId).SingleAsync()).Description);
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
        await context.AuthorizeRoleClaimsAsync();
        var service = context.RolePermissions;

        var result = permissionsOnly
            ? await new UpdateRolePermissions.Handler(context.Roles, context.Db, service).UpdatePermissionsAsync(
                new UpdateRolePermissionsRequest { RoleId = role.Id, Permissions = [] }, CancellationToken.None)
            : await new UpdateRole.Handler(context.Roles, context.Db, service, context.Tenant).UpdateAsync(role.Id,
                new CreateOrUpdateRoleRequest { Name = role.Name!, Permissions = [] }, CancellationToken.None);

        Assert.True(result.IsError);
        context.Db.ChangeTracker.Clear();
        Assert.Equal("Permissions.Users.View", (await context.Db.RoleClaims.Where(claim => claim.RoleId != context.ActorRoleId).SingleAsync()).ClaimValue);
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
        await context.AuthorizeRoleClaimsAsync();
        var service = context.RolePermissions;

        await service.SynchronizeAsync(role.Id, [], CancellationToken.None);
        await context.Db.SaveChangesAsync();

        context.Db.ChangeTracker.Clear();
        var claims = await context.Db.RoleClaims.IgnoreQueryFilters().Where(claim => claim.RoleId != context.ActorRoleId).ToListAsync();
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
        await context.AuthorizeRoleClaimsAsync();
        var service = context.RolePermissions;

        await service.SynchronizeAsync(role.Id,
            ["Permissions.Users.View", "Permissions.Users.View", "Permissions.Users.Create", "Permissions.Users.Create"],
            CancellationToken.None);
        await context.Db.SaveChangesAsync();

        context.Db.ChangeTracker.Clear();
        var claims = await context.Db.RoleClaims.IgnoreQueryFilters().Where(claim => claim.RoleId != context.ActorRoleId).ToListAsync();
        Assert.Equal(3, claims.Count);
        var restored = Assert.Single(claims, c => c.ClaimValue == "Permissions.Users.View");
        Assert.Equal(deletedClaimId, restored.Id);
        Assert.False(restored.IsDeleted);
        Assert.False(Assert.Single(claims, c => c.ClaimValue == "Permissions.Users.Create").IsDeleted);
        Assert.True(Assert.Single(claims, c => c.ClaimValue == "Permissions.Users.Delete").IsDeleted);
    }

    [Theory]
    [InlineData("create", "Permissions.Tenants.Delete", 403)]
    [InlineData("update", "Permissions.Tenants.Delete", 403)]
    [InlineData("permissions", "Permissions.Tenants.Delete", 403)]
    [InlineData("create", "Permissions.Unknown.Grant", 400)]
    [InlineData("update", "Permissions.Unknown.Grant", 400)]
    [InlineData("permissions", "Permissions.Unknown.Grant", 400)]
    [InlineData("create", " ", 400)]
    [InlineData("update", " ", 400)]
    [InlineData("permissions", " ", 400)]
    public async Task EveryRoleWriteRejectsUnauthorizedPermissionsBeforeSavingMetadata(
        string operation, string permission, int expectedStatus)
    {
        await using var context = new IdentityTestContext();
        var role = await context.AddRoleAsync("Editor");
        context.Db.RoleClaims.Add(Claim(role.Id, AppClaimTypes.Permission, "Permissions.Users.View"));
        await context.Db.SaveChangesAsync();

        Response result = await WritePermissionsAsync(context, role.Id, operation, permission);

        Assert.Equal(expectedStatus, result.StatusCode);
        await context.Db.SaveChangesAsync();
        context.Db.ChangeTracker.Clear();
        Assert.Equal("Editor", (await context.Db.Roles.Where(role => role.Id != context.ActorRoleId).SingleAsync()).Name);
        Assert.Equal("Permissions.Users.View", (await context.Db.RoleClaims.Where(claim => claim.RoleId != context.ActorRoleId).SingleAsync()).ClaimValue);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("permissions")]
    public async Task RootTenantCanAssignCatalogRootPermissions(string operation)
    {
        await using var context = new IdentityTestContext();
        context.SetTenant("root");
        var role = await context.AddRoleAsync("Editor");

        Response result = await WritePermissionsAsync(context, role.Id, operation, "Permissions.Tenants.Delete");

        Assert.False(result.IsError, result.Message);
        context.Db.ChangeTracker.Clear();
        Assert.Equal("Permissions.Tenants.Delete", (await context.Db.RoleClaims.Where(claim => claim.RoleId != context.ActorRoleId).SingleAsync()).ClaimValue);
    }

    [Fact]
    public async Task SynchronizationItselfRejectsRootAssignmentsOutsideRootTenant()
    {
        await using var context = new IdentityTestContext();
        var role = await context.AddRoleAsync("Editor");
        await context.AuthorizeRoleClaimsAsync();
        var service = context.RolePermissions;

        Response result = await service.SynchronizeAsync(role.Id, ["Permissions.Tenants.View"], CancellationToken.None);

        Assert.Equal(403, result.StatusCode);
        await context.Db.SaveChangesAsync();
        Assert.Empty(await context.Db.RoleClaims.Where(claim => claim.RoleId != context.ActorRoleId).ToListAsync());
    }

    private static async Task<Response> WritePermissionsAsync(IdentityTestContext context, string roleId,
        string operation, string permission)
    {
        await context.AuthorizeRoleClaimsAsync();
        var service = context.RolePermissions;
        return await (operation switch
        {
            "create" => new CreateRole.Handler(context.Roles, context.Db, service, context.Tenant).CreateAsync(
                new() { Name = "NewRole", Description = "Created", Permissions = [permission] }, CancellationToken.None),
            "update" => new UpdateRole.Handler(context.Roles, context.Db, service, context.Tenant).UpdateAsync(roleId,
                new() { Name = "Renamed", Description = "Changed", Permissions = [permission] }, CancellationToken.None),
            _ => new UpdateRolePermissions.Handler(context.Roles, context.Db, service).UpdatePermissionsAsync(
                new() { RoleId = roleId, Permissions = [permission] }, CancellationToken.None)
        });
    }

    private static ApplicationRoleClaim Claim(string roleId, string type, string value)
        => new() { RoleId = roleId, ClaimType = type, ClaimValue = value };
}
