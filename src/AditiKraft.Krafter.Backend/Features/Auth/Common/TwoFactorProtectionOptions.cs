namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public sealed class TwoFactorProtectionOptions
{
    internal const string ProviderKey = "Krafter.TwoFactor";
    public bool EnrollmentAllowed { get; set; }
}
