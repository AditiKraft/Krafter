using AditiKraft.Krafter.Backend.Common.Entities;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using Microsoft.AspNetCore.Identity;

namespace AditiKraft.Krafter.Backend.Features.Roles.Common;

public class ApplicationRole : IdentityRole<string>, ICommonAuthEntityProperty
{
    public ApplicationRole()
    {
    }

    public ApplicationUser? CreatedBy { get; set; }
    public ApplicationUser? UpdatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? UpdatedById { get; set; }
    public string? Description { get; set; }
    public bool IsDeleted { get; set; }
    public string? DeleteReason { get; set; }

    public ApplicationRole(string name, string? description = null, string? createdById = null)
        : base(name)
    {
        Description = description;
        CreatedById = createdById;
        NormalizedName = name.ToUpperInvariant();
    }

    public string TenantId { get; set; } = null!;
    public virtual ICollection<ApplicationUserRole> UserRoles { get; set; } = new HashSet<ApplicationUserRole>();
}



