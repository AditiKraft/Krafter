using AditiKraft.Krafter.Backend.Common.Entities;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public sealed class TwoFactorRecoveryCode : ITenant
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}
