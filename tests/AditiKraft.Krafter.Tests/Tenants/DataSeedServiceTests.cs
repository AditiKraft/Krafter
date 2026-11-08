using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Tests.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace AditiKraft.Krafter.Tests.Tenants;

public sealed class DataSeedServiceTests
{
    [Fact]
    public async Task FreshRootGetsDefaultOwnerAndCatalogPermissionsExactlyOnce()
    {
        await using var fixture = new IdentityTestContext();
        fixture.SetTenant(SeedDataConstants.RootTenant.Id);

        await Seed(fixture).SeedAsync();
        await Seed(fixture).SeedAsync();

        ApplicationUser owner = await fixture.Db.Users.SingleAsync();
        Assert.Equal(SeedDataConstants.RootUser.Id, owner.Id);
        Assert.Equal(SeedDataConstants.RootUser.EmailAddress, owner.Email);
        Assert.True(owner.IsOwner && owner.IsActive);
        Assert.True(await fixture.Users.CheckPasswordAsync(owner, SeedDataConstants.DefaultPassword));
        Assert.Equal(2, await fixture.Db.UserRoles.CountAsync());
        await AssertPermissionsAsync(fixture, RoleConstants.Basic, PermissionCatalog.Basic);
        await AssertPermissionsAsync(fixture, RoleConstants.Admin, PermissionCatalog.All);
        Assert.Empty(fixture.Jobs.Emails);
    }

    [Fact]
    public async Task ReseedingPreservesRevokedClaimsAndChangedOwnerCredentials()
    {
        await using var fixture = new IdentityTestContext();
        fixture.SetTenant(SeedDataConstants.RootTenant.Id);
        await Seed(fixture).SeedAsync();
        foreach (ApplicationRole role in await fixture.Db.Roles.ToListAsync())
        {
            ApplicationRoleClaim revoked = await fixture.Db.RoleClaims.FirstAsync(claim => claim.RoleId == role.Id);
            fixture.Db.RoleClaims.Remove(revoked);
        }
        await fixture.Db.SaveChangesAsync();
        ApplicationUser owner = await fixture.Db.Users.SingleAsync();
        Assert.True((await fixture.Users.ChangePasswordAsync(owner, SeedDataConstants.DefaultPassword, "ChangedPassword1!")).Succeeded);
        int remainingClaims = await fixture.Db.RoleClaims.CountAsync();
        int historyClaims = await fixture.Db.RoleClaims.IgnoreQueryFilters().CountAsync();

        await Seed(fixture, Environments.Production).SeedAsync();

        Assert.Equal(remainingClaims, await fixture.Db.RoleClaims.CountAsync());
        Assert.Equal(historyClaims, await fixture.Db.RoleClaims.IgnoreQueryFilters().CountAsync());
        Assert.True(await fixture.Users.CheckPasswordAsync(await fixture.Db.Users.SingleAsync(), "ChangedPassword1!"));
    }

    [Theory]
    [InlineData("role", false)]
    [InlineData("role", true)]
    [InlineData("user", false)]
    [InlineData("user", true)]
    public async Task ExistingOrDeletedIdentityHistoryPreventsAutomaticRepair(string history, bool deleted)
    {
        await using var fixture = new IdentityTestContext();
        fixture.SetTenant(SeedDataConstants.RootTenant.Id);
        if (history == "role")
        {
            ApplicationRole role = await fixture.AddRoleAsync(RoleConstants.Admin);
            if (deleted) fixture.Db.Roles.Remove(role);
        }
        else
        {
            ApplicationUser user = await fixture.AddUserAsync();
            if (deleted) fixture.Db.Users.Remove(user);
        }
        await fixture.Db.SaveChangesAsync();

        await Seed(fixture, Environments.Production).SeedAsync();

        Assert.Equal(history == "role" ? 1 : 0, await fixture.Db.Roles.IgnoreQueryFilters().CountAsync());
        Assert.Equal(history == "user" ? 1 : 0, await fixture.Db.Users.IgnoreQueryFilters().CountAsync());
        Assert.Empty(await fixture.Db.RoleClaims.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await fixture.Db.UserRoles.IgnoreQueryFilters().ToListAsync());
        if (deleted)
        {
            Assert.Empty(await fixture.Db.Roles.ToListAsync());
            Assert.Empty(await fixture.Db.Users.ToListAsync());
        }
        Assert.Empty(fixture.Jobs.Emails);
    }

    [Fact]
    public async Task AnotherTenantsHistoryDoesNotSuppressFreshTenantProvisioningOrGrantRootPermissions()
    {
        await using var fixture = new IdentityTestContext();
        fixture.Db.Users.Remove(await fixture.AddUserAsync());
        fixture.Db.Roles.Remove(await fixture.AddRoleAsync(RoleConstants.Admin));
        await fixture.Db.SaveChangesAsync();
        fixture.Tenant.SetTenant(new CurrentTenantDetails
        {
            Id = "new-tenant", AdminEmail = "owner@example.com", TenantLink = "https://new.example.com"
        });
        fixture.TenantDb.Tenants.Add(new Tenant
        {
            Id = "new-tenant", Identifier = "new", AdminEmail = "owner@example.com"
        });
        await fixture.TenantDb.SaveChangesAsync();

        await Seed(fixture).SeedAsync();
        await Seed(fixture).SeedAsync();

        ApplicationUser owner = await fixture.Db.Users.SingleAsync();
        Assert.Equal("new-tenant", owner.TenantId);
        Assert.Equal("owner@example.com", owner.Email);
        Assert.True(owner.IsOwner);
        await AssertPermissionsAsync(fixture, RoleConstants.Basic, PermissionCatalog.Basic);
        await AssertPermissionsAsync(fixture, RoleConstants.Admin, PermissionCatalog.Admin);
        Assert.Equal(2, await fixture.Db.UserRoles.CountAsync());
        var welcome = Assert.Single(fixture.Jobs.Emails);
        Assert.Equal(owner.Email, welcome.Email);
        Assert.Contains("https://new.example.com/login", welcome.HtmlMessage);
        Assert.Equal(2, await fixture.Db.Users.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task PendingChangesCannotBeDiscardedByBootstrap()
    {
        await using var fixture = new IdentityTestContext();
        fixture.Db.Roles.Add(new ApplicationRole("Unsaved") { Id = Guid.NewGuid().ToString() });

        await Assert.ThrowsAsync<InvalidOperationException>(() => Seed(fixture).SeedAsync());

        Assert.True(fixture.Db.ChangeTracker.HasChanges());
        Assert.Empty(await fixture.Db.Users.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData(SeedDataConstants.DefaultPassword)]
    public async Task FreshProductionRootRejectsMissingOrInvalidPasswordBeforeWritingIdentity(string? password)
    {
        await using var fixture = new IdentityTestContext();
        fixture.SetTenant(SeedDataConstants.RootTenant.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Seed(fixture, Environments.Production, password).SeedAsync());

        Assert.Empty(await fixture.Db.Users.ToListAsync());
        Assert.Empty(await fixture.Db.Roles.ToListAsync());
        Assert.Empty(await fixture.Db.RoleClaims.ToListAsync());
        Assert.Empty(fixture.Db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task FreshProductionRootRejectsPasswordLongerThanLoginAllowsWithoutWritingIdentity()
    {
        await using var fixture = new IdentityTestContext();
        fixture.SetTenant(SeedDataConstants.RootTenant.Id);
        string password = new string('a', PasswordLimits.MaximumLength) + "A1!";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Seed(fixture, Environments.Production, password).SeedAsync());

        Assert.Empty(await fixture.Db.Users.ToListAsync());
        Assert.Empty(await fixture.Db.Roles.ToListAsync());
        Assert.Empty(await fixture.Db.RoleClaims.ToListAsync());
        Assert.Empty(fixture.Db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task FreshProductionRootUsesConfiguredPasswordAndExistingHistoryNeedsNoSetting()
    {
        await using var fixture = new IdentityTestContext();
        fixture.SetTenant(SeedDataConstants.RootTenant.Id);
        const string configuredPassword = "BootstrapTestPassword1!";

        await Seed(fixture, Environments.Production, configuredPassword).SeedAsync();
        await Seed(fixture, Environments.Production).SeedAsync();

        ApplicationUser owner = await fixture.Db.Users.SingleAsync();
        Assert.True(await fixture.Users.CheckPasswordAsync(owner, configuredPassword));
        Assert.False(await fixture.Users.CheckPasswordAsync(owner, SeedDataConstants.DefaultPassword));
        fixture.Db.Users.Remove(owner);
        await fixture.Db.SaveChangesAsync();
        await Seed(fixture, Environments.Production).SeedAsync();
        Assert.Empty(await fixture.Db.Users.ToListAsync());
        Assert.Single(await fixture.Db.Users.IgnoreQueryFilters().ToListAsync());
    }

    private static DataSeedService Seed(IdentityTestContext fixture, string environment = "Development",
        string? password = null) => new(
        fixture.Tenant, fixture.Jobs, fixture.Db, fixture.Roles, fixture.Users,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bootstrap:RootPassword"] = password
        }).Build(), new HostingEnvironment { EnvironmentName = environment });

    private static async Task AssertPermissionsAsync(IdentityTestContext fixture, string roleName,
        IReadOnlyList<PermissionDefinition> expected)
    {
        ApplicationRole role = await fixture.Db.Roles.SingleAsync(role => role.Name == roleName);
        string?[] actual = await fixture.Db.RoleClaims.Where(claim => claim.RoleId == role.Id &&
            claim.ClaimType == AppClaimTypes.Permission).Select(claim => claim.ClaimValue).ToArrayAsync();
        Assert.Equal(expected.Select(permission => PermissionDefinition.NameFor(permission.Action, permission.Resource)).Order(),
            actual.Order());
    }
}
