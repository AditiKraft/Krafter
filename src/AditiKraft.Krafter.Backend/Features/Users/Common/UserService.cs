using AditiKraft.Krafter.Backend.Common;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
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

public class UserService(ApplicationDbContext db)
    : IUserService, IScopedService
{
    public async Task<Response<List<string>>> GetPermissionsAsync(string userId, CancellationToken cancellationToken)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return Response<List<string>>.NotFound("User Not Found.");
        }

        // Resolve memberships and their claims in one snapshot by immutable role ID.
        // A role name can move to an unassigned role during concurrent metadata edits.
        List<string> permissions = await (
            from membership in db.UserRoles.AsNoTracking()
            join role in db.Roles on membership.RoleId equals role.Id
            join claim in db.RoleClaims on role.Id equals claim.RoleId
            where membership.UserId == userId && claim.ClaimType == AppClaimTypes.Permission && claim.ClaimValue != null
            select claim.ClaimValue!)
            .Distinct().ToListAsync(cancellationToken);

        return Response<List<string>>.Success(permissions);
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
