using AditiKraft.Krafter.Contracts.Common.Models;

namespace AditiKraft.Krafter.Backend.Common.Tenants;

public interface ITenantSetterService
{
    public void SetTenant(CurrentTenantDetails tenant);
}
