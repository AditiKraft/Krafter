using Backend.Features.Tenants._Shared;
using Krafter.Shared.Common.Models;

namespace Backend.Common.Interfaces;

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
