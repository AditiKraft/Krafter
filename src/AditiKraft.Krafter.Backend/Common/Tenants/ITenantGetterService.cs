using AditiKraft.Krafter.Contracts.Common.Models;

namespace AditiKraft.Krafter.Backend.Common.Tenants;

public interface ITenantGetterService
{
    public CurrentTenantDetails Tenant { get; }
}
