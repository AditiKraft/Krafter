using Backend.Common.Models;
using Backend.Features.Tenants;

namespace Backend.Common.Interfaces;

public interface ITenantFinderService
{
    public Task<Tenant> Find(string? identifier);
}

public interface ITenantGetterService
{
    public CurrentTenantDetails Tenant { get; }
}

public interface ITenantSetterService
{
    public void SetTenant(CurrentTenantDetails tenant);
}
