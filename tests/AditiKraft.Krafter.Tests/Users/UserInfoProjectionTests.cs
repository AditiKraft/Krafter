using System.Text.Json;
using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Features.Roles;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AditiKraft.Krafter.Tests.Users;

public sealed class UserInfoProjectionTests
{
    [Fact]
    public async Task RoleMembersSerializeTheirActualEmail()
    {
        await using var context = new IdentityTestContext();
        var user = await context.AddUserAsync("actual-user@example.test");
        var role = await context.AddRoleAsync("Members");
        context.Db.UserRoles.Add(new ApplicationUserRole { UserId = user.Id, RoleId = role.Id });
        await context.Db.SaveChangesAsync();
        context.Db.ChangeTracker.Clear();

        var response = await new GetUsersByRole.Handler(context.Db).GetByRoleAsync(
            role.Id, new GetRequestInput(), CancellationToken.None);

        Assert.Equal(user.Email, Assert.Single(response.Data!.Items).Email);
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(user.Email, json.RootElement.GetProperty("data").GetProperty("items")[0]
            .GetProperty("email").GetString());
    }

    [Fact]
    public async Task UserAndRoleAuditSummariesUseTheCreatorsAndEditorsEmail()
    {
        await using var context = new IdentityTestContext();
        var creator = await context.AddUserAsync("creator@example.test");
        var editor = await context.AddUserAsync("editor@example.test");
        context.SignIn(creator);
        var user = await context.AddUserAsync("member@example.test");
        var role = await context.AddRoleAsync("Audit role");
        // A different request gets its own CurrentUser and unit of work. Production
        // deliberately refuses to replace an actor after in-scope initialization.
        var editingActor = new CurrentUser();
        editingActor.SetCurrentUserId(editor.Id);
        var options = (DbContextOptions<ApplicationDbContext>)context.Db.GetService<IDbContextOptions>();
        await using (var editingDb = new ApplicationDbContext(options, editingActor, context.Tenant))
        {
            (await editingDb.Users.SingleAsync(candidate => candidate.Id == user.Id)).FirstName = "Updated";
            (await editingDb.Roles.SingleAsync(candidate => candidate.Id == role.Id)).Description = "Updated";
            await editingDb.SaveChangesAsync();
        }
        context.Db.ChangeTracker.Clear();

        var users = await new GetUsers.Handler(context.Db).GetAsync(
            new GetRequestInput { Id = user.Id }, CancellationToken.None);
        var userDto = Assert.Single(users.Data!.Items);
        Assert.Equal(creator.Email, userDto.CreatedBy!.Email);
        Assert.Equal(editor.Email, userDto.UpdatedBy!.Email);
        var roles = await new GetRoles.Handler(context.Db).GetListAsync(
            new GetRequestInput { Id = role.Id }, CancellationToken.None);
        var roleDto = Assert.Single(roles.Data!.Items);
        Assert.Equal(creator.Email, roleDto.CreatedBy!.Email);
        Assert.Equal(editor.Email, roleDto.UpdatedBy!.Email);
        Assert.Equal(string.Empty, new UserInfo().Email);
    }
}
