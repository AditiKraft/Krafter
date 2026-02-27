using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Infrastructure.Persistence.Tenants;

public class TenantFinderService(TenantDbContext tenantDbContext) : ITenantFinderService
{
    public async Task<Response<Tenant>> Find(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Response<Tenant>.Success(KrafterInitialConstants.KrafterTenant);
        }

        Tenant? tenant = await tenantDbContext.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Identifier == identifier);
        if (tenant is null)
        {
            return Response<Tenant>.Success(KrafterInitialConstants.KrafterTenant);
        }

        if (tenant.IsActive == false)
        {
            return Response<Tenant>.BadRequest("Tenant is not active");
        }

        if (tenant.ValidUpto < DateTime.UtcNow)
        {
            return Response<Tenant>.BadRequest("Tenant validity expired");
        }

        return Response<Tenant>.Success(tenant);
    }
}


