namespace AditiKraft.Krafter.Contracts.Contracts.Auth;

public sealed record TwoFactorSetupResponse(string EnrollmentId, string SharedKey, string QrCodeSvg, DateTimeOffset ExpiresAt);
