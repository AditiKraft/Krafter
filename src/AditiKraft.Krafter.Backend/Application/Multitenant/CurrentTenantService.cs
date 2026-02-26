using AditiKraft.Krafter.Backend.Application.Common;
using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Contracts.Common.Models;

namespace AditiKraft.Krafter.Backend.Application.Multitenant;

public class CurrentTenantService : ITenantGetterService, ITenantSetterService
{
    public CurrentTenantDetails Tenant { get; private set; }

    public void SetTenant(CurrentTenantDetails tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant.TenantLink))
        {
            throw new KrafterException("Tenant domain is required");
        }

        Tenant = tenant;
    }
}
