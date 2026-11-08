namespace AditiKraft.Krafter.Contracts.Contracts.Auth;

public sealed record TwoFactorStatusResponse(bool IsEnabled, int RecoveryCodesRemaining, bool HasPassword);
