namespace AditiKraft.Krafter.Backend.Features.Tenants._Shared;

public class Tenant
{
    public string? Id { get; set; }
    public string Identifier { get; set; } = null!;
    public string? Name { get; set; }

    public string AdminEmail { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime ValidUpto { get; set; }
    public DateTime CreatedOn { get; set; }

    public string? DeleteReason { get; set; }
    public bool IsDeleted { get; set; }

    public string? CreatedById { get; set; }

    public string? TablesToCopy { get; set; }
    //does sql server allow storing list of string in one column
}
