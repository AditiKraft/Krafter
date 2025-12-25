using Backend.Entities;
using Microsoft.AspNetCore.Identity;

namespace Backend.Features.Users._Shared;

public class KrafterUser : IdentityUser<string>, ICommonAuthEntityProperty
{
    public bool IsDeleted { get; set; }
    public KrafterUser? CreatedBy { get; set; }
    public KrafterUser? UpdatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? UpdatedById { get; set; }

    [PersonalData] public string? Name { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; }

    public string? DeleteReason { get; set; }

    #region Navigation Properties

    public virtual ICollection<KrafterUserRole> UserRoles { get; set; }

    #endregion Navigation Properties

    public string TenantId { get; set; }
    public bool IsOwner { get; set; }
}
