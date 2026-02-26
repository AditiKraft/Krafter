namespace AditiKraft.Krafter.Contracts.Common.Models;

public class CurrentTenantDetails //: Tenant
{
    public string? Id { get; set; }
    public string? Identifier { get; set; }
    public string? Name { get; set; }

    public string AdminEmail { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime ValidUpto { get; set; }
    public DateTime CreatedOn { get; set; }

    public string? DeleteReason { get; set; }
    public bool IsDeleted { get; set; }

    public string? CreatedById { get; set; }

    public string? TablesToCopy { get; set; }

    public string TenantLink { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserId { get; set; }

    //tenant host address
    public string? Host { get; set; }
}
