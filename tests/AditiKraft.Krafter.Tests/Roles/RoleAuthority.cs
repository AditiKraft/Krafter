using System.Security.Claims;
using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Tests.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Roles;

internal static class RoleAuthority
{
    public static async Task<ApplicationUser> CreateManagerAsync(MfaScope scope)
    {
        var actor = new ApplicationUser
        { Id = Guid.NewGuid().ToString(), UserName = "claim-manager@example.com", Email = "claim-manager@example.com", IsActive = true };
        Assert.True((await scope.Users.CreateAsync(actor, MfaHarness.Password)).Succeeded);
        var role = new ApplicationRole("ClaimManager") { Id = Guid.NewGuid().ToString() };
        Assert.True((await scope.Services.GetRequiredService<RoleManager<ApplicationRole>>().CreateAsync(role)).Succeeded);
        scope.Db.UserRoles.Add(new ApplicationUserRole { UserId = actor.Id, RoleId = role.Id });
        scope.Db.RoleClaims.Add(new ApplicationRoleClaim
        { RoleId = role.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = "Permissions.RoleClaims.Update" });
        await scope.Db.SaveChangesAsync();
        return actor;
    }

    public static RolePermissionService Service(MfaScope scope, ApplicationUser actor)
    {
        var current = (CurrentUser)scope.Services.GetRequiredService<ICurrentUser>();
        current.SetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, actor.Id)], "test")));
        return new RolePermissionService(scope.Db, scope.Services.GetRequiredService<ITenantGetterService>(),
            current, scope.Services.GetRequiredService<IUserService>());
    }
}
