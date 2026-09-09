using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Features.Users.Common;
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
        await context.Db.SaveChangesAsync();
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
        context.Db.ChangeTracker.Clear();
        Assert.Equal("user@example.com", (await context.Db.Users.SingleAsync(u => u.Id == user.Id)).Email);
    }

    private static string ExtractPassword(string htmlMessage)
    {
        const string start = "Your password is:<br/>";
        int offset = htmlMessage.IndexOf(start, StringComparison.Ordinal) + start.Length;
        return htmlMessage[offset..htmlMessage.IndexOf("<br/>", offset, StringComparison.Ordinal)];
    }
}
