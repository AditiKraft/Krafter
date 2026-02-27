using AditiKraft.Krafter.Backend.Entities;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using Microsoft.AspNetCore.Identity;

namespace AditiKraft.Krafter.Backend.Features.Users.Common;

public class KrafterUserRole : IdentityUserRole<string>, ICommonAuthEntityProperty
{
    public KrafterUser? CreatedBy { get; set; }
    public KrafterUser? UpdatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public string? DeleteReason { get; set; }

    public string TenantId { get; set; } = null!;
    public virtual KrafterUser User { get; set; } = null!;
    public virtual KrafterRole Role { get; set; } = null!;
}

public class KrafterUserLogin : IdentityUserLogin<string>
{
    // Add any custom properties or methods if needed
}

public class KrafterUserToken : IdentityUserToken<string>
{
    // Add any custom properties or methods if needed
}


