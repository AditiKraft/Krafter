using Backend.Entities;
using Backend.Features.Roles._Shared;
using Microsoft.AspNetCore.Identity;

namespace Backend.Features.Users._Shared;

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

    public string TenantId { get; set; }
    public virtual KrafterUser User { get; set; }
    public virtual KrafterRole Role { get; set; }
}

public class KrafterUserLogin : IdentityUserLogin<string>
{
    // Add any custom properties or methods if needed
}

public class KrafterUserToken : IdentityUserToken<string>
{
    // Add any custom properties or methods if needed
}
