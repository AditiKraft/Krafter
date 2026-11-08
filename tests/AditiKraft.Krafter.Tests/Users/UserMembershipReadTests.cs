using System.Data.Common;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Roles;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using AditiKraft.Krafter.Tests.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Users;

[Collection("MFA PostgreSQL")]
public sealed class UserMembershipReadTests
{
    [PostgresMfaFact]
    public async Task ConcurrentRoleRenameCannotRemoveAnExistingMembershipFromTheEditorReplacement()
    {
        string actorId = Guid.NewGuid().ToString();
        var interleaver = new BeforeMembershipLookup();
        await using var provider = DomainTestServices.Provider(Guid.NewGuid().ToString(), actorId,
            Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!, identityInterceptor: interleaver);
        await DomainTestServices.MigrateAsync(provider);
        string userId;
        string basicId;
        string assignedId;
        string addedId;
        await using (var seed = provider.CreateAsyncScope())
        {
            var services = seed.ServiceProvider;
            await DomainTestServices.AddUserAsync(services, "actor@example.com", actorId);
            userId = (await DomainTestServices.AddUserAsync(services, "member@example.com")).Id;
            string operatorId = (await DomainTestServices.AddRoleAsync(services, "Operator")).Id;
            basicId = (await DomainTestServices.AddRoleAsync(services, RoleConstants.Basic)).Id;
            assignedId = (await DomainTestServices.AddRoleAsync(services, "Assigned")).Id;
            addedId = (await DomainTestServices.AddRoleAsync(services, "Added")).Id;
            var db = services.GetRequiredService<ApplicationDbContext>();
            db.UserRoles.AddRange(
                new ApplicationUserRole { UserId = actorId, RoleId = operatorId },
                new ApplicationUserRole { UserId = userId, RoleId = basicId },
                new ApplicationUserRole { UserId = userId, RoleId = assignedId });
            foreach (string permission in new[]
                     {
                         "Permissions.Roles.Update", "Permissions.Users.Update",
                         "Permissions.UserRoles.View", "Permissions.UserRoles.Update"
                     })
                db.RoleClaims.Add(new ApplicationRoleClaim
                    { RoleId = operatorId, ClaimType = AppClaimTypes.Permission, ClaimValue = permission });
            await db.SaveChangesAsync();
        }

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var request = provider.CreateAsyncScope();
        interleaver.Target = request.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        interleaver.BeforeLookup = async () =>
        {
            await using var rename = provider.CreateAsyncScope();
            var services = rename.ServiceProvider;
            var handler = new UpdateRole.Handler(services.GetRequiredService<RoleManager<ApplicationRole>>(),
                services.GetRequiredService<ApplicationDbContext>(), services.GetRequiredService<RolePermissionService>(),
                services.GetRequiredService<ITenantGetterService>());
            Response result = await handler.UpdateAsync(assignedId,
                new() { Name = "Renamed", Description = "Metadata only", Permissions = null }, deadline.Token);
            Assert.False(result.IsError, result.Message);
        };

        Response<List<UserRoleDto>> memberships = await ActivatorUtilities
            .CreateInstance<GetUserRoles.Handler>(request.ServiceProvider).GetRolesAsync(userId, deadline.Token);
        Assert.True(interleaver.Interleaved);
        Assert.False(memberships.IsError, memberships.Message);

        // The editor starts from enabled memberships and adds the newly selected ID.
        // Carry that actual endpoint result through the real replacement operation.
        List<string> replacement = memberships.Data!.Where(role => role.Enabled)
            .Select(role => role.RoleId!).Append(addedId).ToList();
        Response saved = await request.ServiceProvider.GetRequiredService<IUserMutationService>().UpdateAsync(userId,
            new CreateUserRequest
            {
                FirstName = "Member", LastName = "User", Email = "member@example.com", Roles = replacement
            }, deadline.Token);
        Assert.False(saved.IsError, saved.Message);

        await using var verify = provider.CreateAsyncScope();
        var verifiedDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string[] actual = await verifiedDb.UserRoles.Where(link => link.UserId == userId)
            .OrderBy(link => link.RoleId).Select(link => link.RoleId).ToArrayAsync(deadline.Token);
        Assert.Equal(new[] { basicId, assignedId, addedId }.Order(StringComparer.Ordinal), actual);
        UserRoleDto renamed = Assert.Single(memberships.Data!, role => role.RoleId == assignedId);
        Assert.Equal("Renamed", renamed.RoleName);
        Assert.True(renamed.Enabled);
        Assert.Equal("Renamed", (await verifiedDb.Roles.SingleAsync(role => role.Id == assignedId, deadline.Token)).Name);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("membership")]
    [InlineData("role")]
    [InlineData("user")]
    [InlineData("tenant")]
    public async Task MembershipReadsKeepDeletionAndTenantFilters(string change)
    {
        await using var provider = DomainTestServices.Provider(Guid.NewGuid().ToString());
        string userId;
        string assignedId;
        await using (var seed = provider.CreateAsyncScope())
        {
            var services = seed.ServiceProvider;
            userId = (await DomainTestServices.AddUserAsync(services, "member@example.com")).Id;
            assignedId = (await DomainTestServices.AddRoleAsync(services, "Assigned")).Id;
            await DomainTestServices.AddRoleAsync(services, "Unassigned");
            var db = services.GetRequiredService<ApplicationDbContext>();
            db.UserRoles.Add(new ApplicationUserRole { UserId = userId, RoleId = assignedId });
            await db.SaveChangesAsync();
            switch (change)
            {
                case "membership": (await db.UserRoles.SingleAsync()).IsDeleted = true; break;
                case "role": (await db.Roles.SingleAsync(role => role.Id == assignedId)).IsDeleted = true; break;
                case "user": (await db.Users.SingleAsync()).IsDeleted = true; break;
            }
            await db.SaveChangesAsync();
        }

        await using var request = provider.CreateAsyncScope();
        if (change == "tenant")
        {
            DomainTestServices.SelectTenant(request.ServiceProvider, Guid.NewGuid().ToString());
            string foreignUser = (await DomainTestServices.AddUserAsync(request.ServiceProvider, "member@example.com")).Id;
            string foreignRole = (await DomainTestServices.AddRoleAsync(request.ServiceProvider, "Assigned")).Id;
            var foreignDb = request.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreignDb.UserRoles.Add(new ApplicationUserRole { UserId = foreignUser, RoleId = foreignRole });
            await foreignDb.SaveChangesAsync();
        }

        Response<List<UserRoleDto>> result = await ActivatorUtilities
            .CreateInstance<GetUserRoles.Handler>(request.ServiceProvider).GetRolesAsync(userId, CancellationToken.None);
        Assert.Equal(change is "user" or "tenant" ? 404 : 200, result.StatusCode);
        if (change == "none")
        {
            UserRoleDto membership = Assert.Single(result.Data!);
            Assert.Equal(assignedId, membership.RoleId);
            Assert.Equal("Assigned", membership.RoleName);
            Assert.True(membership.Enabled);
        }
        else if (!result.IsError)
        {
            Assert.Empty(result.Data!);
        }
    }

    private sealed class BeforeMembershipLookup : DbCommandInterceptor
    {
        public DbContext? Target { get; set; }
        public Func<Task>? BeforeLookup { get; set; }
        public bool Interleaved { get; private set; }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            // Leave Identity's initial membership-name query untouched. The old second
            // lookup reads roles alone; the corrected projection reads RoleName by ID.
            bool membershipLookup = command.CommandText.Contains("\"ApplicationRole\"", StringComparison.Ordinal) &&
                (!command.CommandText.Contains("\"ApplicationUserRole\"", StringComparison.Ordinal) ||
                 command.CommandText.Contains("AS \"RoleName\"", StringComparison.Ordinal));
            if (ReferenceEquals(eventData.Context, Target) && membershipLookup && BeforeLookup is { } action)
            {
                BeforeLookup = null;
                await action();
                Interleaved = true;
            }
            return result;
        }
    }
}
