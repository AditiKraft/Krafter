using Backend.Features.Tenants;

namespace Backend.Common.Models;

public class CurrentTenantDetails : Tenant
{
    public string TenantLink { get; set; }
    public string? IpAddress { get; set; }
    public string? UserId { get; set; }

    //tenant host address
    public string? Host { get; set; }
}
