using AditiKraft.Krafter.Backend.Features.Tenants._Shared;
using AditiKraft.Krafter.Shared.Common.Models;

namespace AditiKraft.Krafter.Backend.Common.Interfaces;

public interface ITenantFinderService
{
    public Task<Response<Tenant>> Find(string? identifier);
}

public interface ITenantGetterService
{
    public CurrentTenantDetails Tenant { get; }
}

public interface ITenantSetterService
{
    public void SetTenant(CurrentTenantDetails tenant);
}
