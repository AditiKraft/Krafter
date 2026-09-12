using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Auth;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Roles.Common;

public sealed class RolePermissionService(ApplicationDbContext db, ITenantGetterService tenantGetterService)
{
    public async Task SynchronizeAsync(string roleId, IReadOnlyCollection<string> requestedPermissions,
        CancellationToken cancellationToken)
    {
        HashSet<string> requested = requestedPermissions.Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.Ordinal);
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
    }
}
