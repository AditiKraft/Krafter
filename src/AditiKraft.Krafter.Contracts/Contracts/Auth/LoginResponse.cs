namespace AditiKraft.Krafter.Contracts.Contracts.Auth;

public sealed record LoginResponse(TokenResponse? Tokens, TwoFactorChallengeResponse? Challenge)
{
    public bool IsCompleted => Tokens is not null && Challenge is null;
    public bool RequiresTwoFactor => Tokens is null && Challenge is not null;
    public static LoginResponse Completed(TokenResponse tokens) => new(tokens, null);
    public static LoginResponse Pending(string id, DateTimeOffset expires) => new(null, new(id, expires));
}

public sealed record TwoFactorChallengeResponse(string Id, DateTimeOffset ExpiresAt);
public sealed record TwoFactorStatusResponse(bool IsEnabled, int RecoveryCodesRemaining, bool HasPassword);
public sealed record TwoFactorSetupResponse(string EnrollmentId, string SharedKey, string QrCodeSvg, DateTimeOffset ExpiresAt);
public sealed record RecoveryCodesResponse(IReadOnlyList<string> Codes);
