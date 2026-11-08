using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Tests.Users;

public sealed class UserMutationTests
{
    [Fact]
    public async Task CreateAssignsBasicOnceAndQueuesEmailAfterSavingRoles()
    {
        await using var context = new IdentityTestContext();
        var basic = await context.AddRoleAsync(RoleConstants.Basic);
        var request = new CreateUserRequest
        {
            FirstName = "New", LastName = "User", Email = "new@example.com", Roles = [basic.Id, basic.Id]
        };

        var result = await new CreateUser.Handler(context.Mutations).CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsError, result.Message);
        context.Db.ChangeTracker.Clear();
        var user = await context.Db.Users.SingleAsync();
        Assert.Equal("new@example.com", user.UserName);
        Assert.True(await context.Users.CheckPasswordAsync(user,
            ExtractPassword(Assert.Single(context.Jobs.Emails).HtmlMessage)));
        var assignment = await context.Db.UserRoles.SingleAsync();
        Assert.Equal(user.Id, assignment.UserId);
        Assert.Equal(basic.Id, assignment.RoleId);
        Assert.Equal("tenant-a", assignment.TenantId);
    }

    [Fact]
    public async Task UpdateProfileUsesSharedSaveAndRestoresRoleAssignments()
    {
        await using var context = new IdentityTestContext();
        var basic = await context.AddRoleAsync(RoleConstants.Basic);
        var editor = await context.AddRoleAsync("Editor");
        var user = await context.AddUserAsync();
        context.Db.UserRoles.AddRange(
            new ApplicationUserRole { UserId = user.Id, RoleId = basic.Id },
            new ApplicationUserRole { UserId = user.Id, RoleId = editor.Id, IsDeleted = true });
        context.Db.RoleClaims.Add(new ApplicationRoleClaim
        { RoleId = basic.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = "Permissions.UserRoles.Update" });
        await context.Db.SaveChangesAsync();
        context.SignIn(user);
        context.TenantDb.Tenants.AddRange(
            new Tenant { Id = "tenant-a", Identifier = "a", Name = "A", AdminEmail = user.Email! },
            new Tenant { Id = "tenant-b", Identifier = "b", Name = "B", AdminEmail = user.Email! });
        await context.TenantDb.SaveChangesAsync();
        string? passwordHash = user.PasswordHash;

        var result = await new UpdateUser.Handler(context.Mutations).UpdateAsync(user.Id, new CreateUserRequest
        {
            FirstName = "Changed", LastName = "Name", PhoneNumber = null, Email = "changed@example.com",
            Roles = [editor.Id, editor.Id]
        }, CancellationToken.None);

        Assert.False(result.IsError, result.Message);
        context.Db.ChangeTracker.Clear();
        user = await context.Db.Users.SingleAsync();
        Assert.Equal("Changed", user.FirstName);
        Assert.Equal("Name", user.LastName);
        Assert.Null(user.PhoneNumber);
        Assert.Equal("CHANGED@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Equal(passwordHash, user.PasswordHash);
        var assignments = await context.Db.UserRoles.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(2, assignments.Count);
        Assert.True(assignments.Single(role => role.RoleId == basic.Id).IsDeleted);
        Assert.False(assignments.Single(role => role.RoleId == editor.Id).IsDeleted);
        Assert.Equal("changed@example.com", (await context.TenantDb.Tenants.SingleAsync(t => t.Id == "tenant-a")).AdminEmail);
        Assert.Equal("user@example.com", (await context.TenantDb.Tenants.SingleAsync(t => t.Id == "tenant-b")).AdminEmail);
        Assert.Empty(context.Jobs.Emails);
    }

    [Fact]
    public async Task TenantEmailUpdatePreservesProfilePasswordAndRoles()
    {
        await using var context = new IdentityTestContext();
        var admin = await context.AddRoleAsync(RoleConstants.Admin);
        var user = await context.AddUserAsync();
        context.Db.UserRoles.Add(new ApplicationUserRole { UserId = user.Id, RoleId = admin.Id });
        await context.Db.SaveChangesAsync();
        string? passwordHash = user.PasswordHash;

        var result = await context.Mutations.UpdateEmailAsync(user.Id, "admin.changed@example.com", CancellationToken.None);

        Assert.False(result.IsError, result.Message);
        context.Db.ChangeTracker.Clear();
        user = await context.Db.Users.SingleAsync();
        Assert.Equal("First", user.FirstName);
        Assert.Equal("Last", user.LastName);
        Assert.Equal("123456", user.PhoneNumber);
        Assert.Equal("admin.changed@example.com", user.Email);
        Assert.Equal("ADMIN.CHANGED@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Equal("ADMIN.CHANGED@EXAMPLE.COM", user.NormalizedUserName);
        Assert.Equal(passwordHash, user.PasswordHash);
        Assert.Equal(admin.Id, (await context.Db.UserRoles.SingleAsync()).RoleId);
        Assert.Empty(context.Jobs.Emails);
    }

    [Fact]
    public async Task ForeignTenantRoleIsRejectedBeforeUserChangesAreSaved()
    {
        await using var context = new IdentityTestContext();
        var user = await context.AddUserAsync();
        context.SetTenant("tenant-b");
        var foreignRole = await context.AddRoleAsync("Foreign");
        context.SetTenant("tenant-a");
        context.Db.ChangeTracker.Clear();

        var result = await context.Mutations.UpdateAsync(user.Id, new CreateUserRequest
        {
            FirstName = "Changed", LastName = "Name", Email = "changed@example.com", Roles = [foreignRole.Id]
        }, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(400, result.StatusCode);
        context.Db.ChangeTracker.Clear();
        user = await context.Db.Users.SingleAsync();
        Assert.Equal("First", user.FirstName);
        Assert.Equal("user@example.com", user.Email);
        Assert.Empty(await context.Db.UserRoles.ToListAsync());
    }

    [Fact]
    public async Task FailedEmailUpdateReturnsIdentityErrorsAndDoesNotSaveNewEmail()
    {
        await using var context = new IdentityTestContext();
        var user = await context.AddUserAsync();
        await context.AddUserAsync("taken@example.com");

        var result = await context.Mutations.UpdateEmailAsync(user.Id, "taken@example.com", CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("taken", result.Message!, StringComparison.OrdinalIgnoreCase);
        await context.Db.SaveChangesAsync();
        context.Db.ChangeTracker.Clear();
        Assert.Equal("user@example.com", (await context.Db.Users.SingleAsync(u => u.Id == user.Id)).Email);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangingMembershipRequiresTheSeparateRoleAssignmentPermission(bool mayChangeRoles)
    {
        await using var context = new IdentityTestContext();
        var editor = await context.AddRoleAsync("Editor");
        var admin = await context.AddRoleAsync(RoleConstants.Admin);
        var user = await context.AddUserAsync();
        context.Db.UserRoles.Add(new ApplicationUserRole { UserId = user.Id, RoleId = editor.Id });
        context.Db.RoleClaims.Add(new ApplicationRoleClaim
        { RoleId = editor.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = "Permissions.Users.Update" });
        if (mayChangeRoles)
        {
            context.Db.RoleClaims.Add(new ApplicationRoleClaim
            { RoleId = editor.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = "Permissions.UserRoles.Update" });
        }
        await context.Db.SaveChangesAsync();
        context.SignIn(user);

        var result = await new UpdateUser.Handler(context.Mutations).UpdateAsync(user.Id, new CreateUserRequest
        {
            FirstName = "Changed", LastName = user.LastName!, Email = user.Email!, Roles = [admin.Id]
        }, CancellationToken.None);

        Assert.Equal(mayChangeRoles ? 200 : 403, result.StatusCode);
        context.Db.ChangeTracker.Clear();
        Assert.Equal(mayChangeRoles ? admin.Id : editor.Id, (await context.Db.UserRoles.SingleAsync()).RoleId);
        Assert.Equal(mayChangeRoles ? "Changed" : "First", (await context.Db.Users.SingleAsync()).FirstName);
    }

    [Theory]
    [InlineData("omitted")]
    [InlineData("empty")]
    [InlineData("unchanged")]
    public async Task ProfileEditsPreserveRolesWithoutRoleAssignmentAuthority(string submittedRoles)
    {
        await using var context = new IdentityTestContext();
        var role = await context.AddRoleAsync("Editor");
        var user = await context.AddUserAsync();
        context.Db.UserRoles.Add(new ApplicationUserRole { UserId = user.Id, RoleId = role.Id });
        await context.Db.SaveChangesAsync();
        context.SignIn(user);

        var result = await context.Mutations.UpdateAsync(user.Id, new CreateUserRequest
        {
            FirstName = "Changed", LastName = user.LastName!, Email = user.Email!,
            Roles = submittedRoles == "omitted" ? null! : submittedRoles == "empty" ? [] : [role.Id, role.Id]
        }, CancellationToken.None);

        Assert.False(result.IsError, result.Message);
        context.Db.ChangeTracker.Clear();
        Assert.Equal("Changed", (await context.Db.Users.SingleAsync()).FirstName);
        Assert.Equal(role.Id, (await context.Db.UserRoles.SingleAsync()).RoleId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ChangingAnotherUsersLoginEmailRequiresRoleAssignmentAuthority(
        bool mayChangeRoles, bool submitUnchangedRoles)
    {
        await using var context = new IdentityTestContext();
        var editor = await context.AddRoleAsync("Editor");
        var privilegedRole = await context.AddRoleAsync("PrivilegedOperator");
        var actor = await context.AddUserAsync("editor@example.com");
        var target = await context.AddUserAsync("operator@example.com");
        context.Db.UserRoles.AddRange(
            new ApplicationUserRole { UserId = actor.Id, RoleId = editor.Id },
            new ApplicationUserRole { UserId = target.Id, RoleId = privilegedRole.Id });
        context.Db.RoleClaims.AddRange(
            new ApplicationRoleClaim
            { RoleId = editor.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = "Permissions.Users.Update" },
            new ApplicationRoleClaim
            { RoleId = privilegedRole.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = "Permissions.Roles.Update" });
        if (mayChangeRoles)
        {
            context.Db.RoleClaims.Add(new ApplicationRoleClaim
            { RoleId = editor.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = "Permissions.UserRoles.Update" });
        }
        await context.Db.SaveChangesAsync();
        context.TenantDb.Tenants.Add(new Tenant
        { Id = "tenant-a", Identifier = "a", Name = "A", AdminEmail = target.Email! });
        await context.TenantDb.SaveChangesAsync();
        context.SignIn(actor);
        string? originalStamp = target.ConcurrencyStamp;

        var result = await new UpdateUser.Handler(context.Mutations).UpdateAsync(target.Id, new CreateUserRequest
        {
            FirstName = "Changed", LastName = "Name", PhoneNumber = "654321", Email = "new.owner@example.com",
            Roles = submitUnchangedRoles ? [privilegedRole.Id, privilegedRole.Id] : null!
        }, CancellationToken.None);

        Assert.Equal(mayChangeRoles ? 200 : 403, result.StatusCode);
        Assert.False(context.Db.ChangeTracker.HasChanges());
        Assert.False(context.TenantDb.ChangeTracker.HasChanges());
        Assert.Equal(0, await context.Db.SaveChangesAsync());
        Assert.Equal(0, await context.TenantDb.SaveChangesAsync());
        context.Db.ChangeTracker.Clear();
        context.TenantDb.ChangeTracker.Clear();
        target = await context.Db.Users.SingleAsync(user => user.Id == target.Id);
        string expectedEmail = mayChangeRoles ? "new.owner@example.com" : "operator@example.com";
        Assert.Equal(expectedEmail, target.Email);
        Assert.Equal(expectedEmail, target.UserName);
        Assert.Equal(context.Users.NormalizeEmail(expectedEmail), target.NormalizedEmail);
        Assert.Equal(context.Users.NormalizeName(expectedEmail), target.NormalizedUserName);
        Assert.Equal(mayChangeRoles ? "Changed" : "First", target.FirstName);
        Assert.Equal(mayChangeRoles ? "Name" : "Last", target.LastName);
        Assert.Equal(mayChangeRoles ? "654321" : "123456", target.PhoneNumber);
        if (!mayChangeRoles)
        {
            Assert.Equal(originalStamp, target.ConcurrencyStamp);
        }
        Assert.Equal(privilegedRole.Id,
            (await context.Db.UserRoles.SingleAsync(role => role.UserId == target.Id)).RoleId);
        Assert.Equal(expectedEmail, (await context.TenantDb.Tenants.SingleAsync()).AdminEmail);
        Assert.Equal("editor@example.com", (await context.Db.Users.SingleAsync(user => user.Id == actor.Id)).Email);
        Assert.Empty(context.Jobs.Emails);
    }

    [Fact]
    public async Task SelfEmailChangeDoesNotRequireRoleAssignmentAuthority()
    {
        await using var context = new IdentityTestContext();
        var role = await context.AddRoleAsync("Editor");
        var user = await context.AddUserAsync();
        context.Db.UserRoles.Add(new ApplicationUserRole { UserId = user.Id, RoleId = role.Id });
        await context.Db.SaveChangesAsync();
        context.SignIn(user);

        var result = await context.Mutations.UpdateAsync(user.Id, new CreateUserRequest
        { FirstName = "Changed", LastName = user.LastName!, Email = "self.changed@example.com", Roles = [role.Id] },
            CancellationToken.None);

        Assert.False(result.IsError, result.Message);
        context.Db.ChangeTracker.Clear();
        user = await context.Db.Users.SingleAsync();
        Assert.Equal("self.changed@example.com", user.Email);
        Assert.Equal("SELF.CHANGED@EXAMPLE.COM", user.NormalizedUserName);
        Assert.Equal("Changed", user.FirstName);
        Assert.Equal(role.Id, (await context.Db.UserRoles.SingleAsync()).RoleId);
        Assert.Empty(context.Jobs.Emails);
    }

    [Theory]
    [InlineData("operator@example.com")]
    [InlineData("OPERATOR@EXAMPLE.COM")]
    public async Task OtherUserProfileEditsWithEquivalentLoginEmailDoNotRequireRoleAssignmentAuthority(string email)
    {
        await using var context = new IdentityTestContext();
        var role = await context.AddRoleAsync("PrivilegedOperator");
        var actor = await context.AddUserAsync("editor@example.com");
        var target = await context.AddUserAsync("operator@example.com");
        context.Db.UserRoles.Add(new ApplicationUserRole { UserId = target.Id, RoleId = role.Id });
        await context.Db.SaveChangesAsync();
        context.SignIn(actor);

        var result = await context.Mutations.UpdateAsync(target.Id, new CreateUserRequest
        { FirstName = "Changed", LastName = target.LastName!, Email = email, Roles = [role.Id] }, CancellationToken.None);

        Assert.False(result.IsError, result.Message);
        context.Db.ChangeTracker.Clear();
        target = await context.Db.Users.SingleAsync(user => user.Id == target.Id);
        Assert.Equal(email, target.Email);
        Assert.Equal("OPERATOR@EXAMPLE.COM", target.NormalizedEmail);
        Assert.Equal("OPERATOR@EXAMPLE.COM", target.NormalizedUserName);
        Assert.Equal("Changed", target.FirstName);
        Assert.Equal(role.Id, (await context.Db.UserRoles.SingleAsync()).RoleId);
        Assert.Empty(context.Jobs.Emails);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreatingWithAdditionalRolesRequiresRoleAssignmentAuthority(bool mayChangeRoles)
    {
        await using var context = new IdentityTestContext();
        var basic = await context.AddRoleAsync(RoleConstants.Basic);
        var admin = await context.AddRoleAsync(RoleConstants.Admin);
        var actor = await context.AddUserAsync();
        context.Db.UserRoles.Add(new ApplicationUserRole { UserId = actor.Id, RoleId = basic.Id });
        if (mayChangeRoles)
        {
            context.Db.RoleClaims.Add(new ApplicationRoleClaim
            { RoleId = basic.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = "Permissions.UserRoles.Update" });
        }
        await context.Db.SaveChangesAsync();
        context.SignIn(actor);

        var result = await context.Mutations.CreateAsync(new CreateUserRequest
        { FirstName = "New", LastName = "User", Email = "new@example.com", Roles = [admin.Id] }, CancellationToken.None);

        Assert.Equal(mayChangeRoles ? 200 : 403, result.StatusCode);
        context.Db.ChangeTracker.Clear();
        var created = await context.Db.Users.SingleOrDefaultAsync(user => user.Email == "new@example.com");
        Assert.Equal(mayChangeRoles, created is not null);
        if (created is not null)
        {
            Assert.Equal(2, await context.Db.UserRoles.CountAsync(link => link.UserId == created.Id));
        }
        Assert.Equal(mayChangeRoles ? 1 : 0, context.Jobs.Emails.Count);
    }

    private static string ExtractPassword(string htmlMessage)
    {
        const string start = "Your password is:<br/>";
        int offset = htmlMessage.IndexOf(start, StringComparison.Ordinal) + start.Length;
        return htmlMessage[offset..htmlMessage.IndexOf("<br/>", offset, StringComparison.Ordinal)];
    }
}
