namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

// Global orchestration state: Google returns to the root host before the tenant is restored.
// Verifiers and completed credentials are protected with purpose-specific Data Protection.
public sealed class GoogleLoginTransaction
{
    public string Id { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string Purpose { get; set; } = default!;
    public string BrowserProofHash { get; set; } = default!;
    public string ProtectedPayload { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
}
