using AditiKraft.Krafter.Backend.Common.Entities;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public sealed class TwoFactorCredential : ITenant
{
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string? ProtectedKey { get; set; }
    public long LastAcceptedStep { get; set; } = -1;
    public bool IsDeleted { get; set; }
}
