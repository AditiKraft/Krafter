using Backend.Entities;
using Backend.Features.Users._Shared;
using Microsoft.AspNetCore.Identity;

namespace Backend.Features.Roles._Shared;

public class KrafterRole : IdentityRole<string>, ICommonAuthEntityProperty
{
    public KrafterRole()
    {
    }

    public KrafterUser? CreatedBy { get; set; }
    public KrafterUser? UpdatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? UpdatedById { get; set; }
    public string? Description { get; set; }
    public bool IsDeleted { get; set; }
    public string? DeleteReason { get; set; }

    public KrafterRole(string name, string? description = null, string? createdById = null)
        : base(name)
    {
        Description = description;
        CreatedById = createdById;
        NormalizedName = name.ToUpperInvariant();
    }

    public string TenantId { get; set; }
    public virtual ICollection<KrafterUserRole> UserRoles { get; set; }
}
