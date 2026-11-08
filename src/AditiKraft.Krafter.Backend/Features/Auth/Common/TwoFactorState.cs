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

public sealed class TwoFactorChallenge : ITenant
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string? PrimaryMethod { get; set; }
    public int AuthenticationVersion { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public string? ProtectedKey { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class TwoFactorRecoveryCode : ITenant
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}
