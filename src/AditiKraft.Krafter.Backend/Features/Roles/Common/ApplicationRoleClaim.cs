using AditiKraft.Krafter.Backend.Common.Entities;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using Microsoft.AspNetCore.Identity;

namespace AditiKraft.Krafter.Backend.Features.Roles.Common;

public class ApplicationRoleClaim : IdentityRoleClaim<string>, ICommonAuthEntityProperty
{
    public ApplicationUser? CreatedBy { get; set; }
    public ApplicationUser? UpdatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public string? DeleteReason { get; set; }

    public string TenantId { get; set; } = null!;
}



