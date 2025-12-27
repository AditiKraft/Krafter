using Backend.Application.Common;
using Backend.Common.Interfaces;
using Krafter.Shared.Common.Models;

namespace Backend.Application.Multitenant;

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
