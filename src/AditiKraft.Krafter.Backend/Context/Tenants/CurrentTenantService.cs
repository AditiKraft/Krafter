using AditiKraft.Krafter.Backend.Errors;
using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using Mapster;

namespace AditiKraft.Krafter.Backend.Context.Tenants;

public class CurrentTenantService : ITenantGetterService, ITenantSetterService
{
    public CurrentTenantDetails Tenant { get; private set; } =
        KrafterInitialConstants.KrafterTenant.Adapt<CurrentTenantDetails>();

    public void SetTenant(CurrentTenantDetails tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant.TenantLink))
        {
            throw new KrafterException("Tenant domain is required");
        }

        Tenant = tenant;
    }
}



