using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Roles.Common;

public sealed class RolePermissionService(ApplicationDbContext db, ITenantGetterService tenantGetterService)
{
    public Response ValidatePermissions(IReadOnlyCollection<string>? requestedPermissions)
    {
        if (requestedPermissions is null)
        {
            return Response.BadRequest("Permissions list is required.");
        }

        foreach (string requested in requestedPermissions)
        {
            PermissionDefinition? permission = PermissionCatalog.All.FirstOrDefault(p => p.Name == requested);
            if (permission is null)
            {
                return Response.BadRequest("One or more permissions were not found in the permission catalog.");
            }

            if (permission.IsRoot && tenantGetterService.Tenant.Id != SeedDataConstants.RootTenant.Id)
            {
                return Response.Forbidden("Root permissions can only be assigned in the root tenant.");
            }
        }

        return Response.Success();
    }

    public async Task<Response> SynchronizeAsync(string roleId, IReadOnlyCollection<string> requestedPermissions,
        CancellationToken cancellationToken)
    {
        Response validation = ValidatePermissions(requestedPermissions);
        if (validation.IsError)
        {
            return validation;
        }

        HashSet<string> requested = requestedPermissions.ToHashSet(StringComparer.Ordinal);
        List<ApplicationRoleClaim> existing = await db.RoleClaims.IgnoreQueryFilters()
            .Where(claim => claim.TenantId == tenantGetterService.Tenant.Id && claim.RoleId == roleId &&
                            claim.ClaimType == AppClaimTypes.Permission)
            .ToListAsync(cancellationToken);

        foreach (ApplicationRoleClaim claim in existing)
        {
            claim.IsDeleted = claim.ClaimValue is null || !requested.Remove(claim.ClaimValue);
        }

        db.RoleClaims.AddRange(requested.Select(permission => new ApplicationRoleClaim
        {
            RoleId = roleId, ClaimType = AppClaimTypes.Permission, ClaimValue = permission
        }));

        return Response.Success();
    }
}
