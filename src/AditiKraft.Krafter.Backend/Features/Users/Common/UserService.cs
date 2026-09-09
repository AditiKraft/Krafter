using AditiKraft.Krafter.Backend.Common;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Users.Common;

public static class QueryStringKeys
{
    public const string Code = "code";
    public const string UserId = "userId";
}

public static class SeedDataConstants
{
    public static class RootUser
    {
        public const string Id = "root";
        public const string LastName = "Admin";
        public const string FirstName = "Admin";
        public const string EmailAddress = "admin@example.com";
    }

    public static class RootTenant
    {
        public const string Id = "root";

        public const string Identifier = DefaultTenantConstants.Identifier;
        public const string Name = DefaultTenantConstants.Name;
    }

    public const string DefaultPassword = "123Pa$$word!";

    public static Tenant DefaultTenant { private set; get; } = new()
    {
        Id = RootTenant.Id,
        Identifier = RootTenant.Identifier,
        IsActive = true,
        Name = RootTenant.Name,
        CreatedOn = DateTime.UtcNow,
        ValidUpto = DateTime.MaxValue,
        AdminEmail = RootUser.EmailAddress
    };
}

public class UserService(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    ApplicationDbContext db)
    : IUserService, IScopedService
{
    public async Task<Response<List<string>>> GetPermissionsAsync(string userId, CancellationToken cancellationToken)
    {
        //var user = await userManager.Asn.FindByIdAsync(userId);
        ApplicationUser? user = await db.Users.AsNoTracking().FirstOrDefaultAsync(c => c.Id == userId, cancellationToken);
        if (user is null)
        {
            return Response<List<string>>.NotFound("User Not Found.");
        }

        IList<string> userRoles = await userManager.GetRolesAsync(user);
        var permissions = new List<string>();
        foreach (ApplicationRole role in await roleManager.Roles.AsNoTracking()
                     .Where(r => userRoles.Contains(r.Name!) && r.IsDeleted == false)
                     .ToListAsync(cancellationToken))
        {
            permissions.AddRange(await db.RoleClaims.AsNoTracking()
                .Where(rc =>
                    rc.RoleId == role.Id && rc.ClaimType == AppClaimTypes.Permission && rc.IsDeleted == false)
                .Select(rc => rc.ClaimValue!)
                .ToListAsync(cancellationToken));
        }

        return Response<List<string>>.Success(permissions.Distinct().ToList());
    }

    public async Task<Response<bool>> HasPermissionAsync(string userId, string permission,
        CancellationToken cancellationToken)
    {
        Response<List<string>>? permissions = await GetPermissionsAsync(userId, cancellationToken);
        if (permissions.IsError)
        {
            return Response<bool>.NotFound(permissions.Message ?? "User Not Found.");
        }

        return Response<bool>.Success(permissions?.Data?.Contains(permission) ?? false);
    }

}
