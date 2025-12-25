using Backend.Application.Common;
using Backend.Common.Interfaces;
using Backend.Features.Tenants;
using Backend.Features.Users._Shared;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Tenants;

public class TenantFinderService(TenantDbContext tenantDbContext) : ITenantFinderService
{
    public async Task<Tenant> Find(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return KrafterInitialConstants.KrafterTenant;
        }

        Tenant? tenant = await tenantDbContext.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Identifier == identifier);
        if (tenant is null)
        {
            return KrafterInitialConstants.KrafterTenant;
        }

        if (tenant.IsActive == false)
        {
            throw new KrafterException("Tenant is not active");
        }

        if (tenant.ValidUpto < DateTime.UtcNow)
        {
            throw new KrafterException("Tenant validity expired");
        }

        return tenant;
    }
}
