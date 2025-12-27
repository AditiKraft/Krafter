using Krafter.Shared.Common.Models;

namespace Krafter.Shared.Features.Tenants;

public sealed class Get
{
    public sealed class TenantDto
    {
        public string? Id { get; set; }
        public string? Identifier { get; set; }
        public string? Name { get; set; }

        public string AdminEmail { get; set; } = default!;
        public bool IsActive { get; set; }
        public DateTime ValidUpto { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public DateTime? PeriodStart { get; set; }
        public UserInfo CreatedBy { get; set; }
        public string? CreatedById { get; set; }

        public string? DeleteReason { get; set; }
    }
}
