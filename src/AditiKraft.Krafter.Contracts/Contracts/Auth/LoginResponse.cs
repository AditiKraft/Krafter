namespace AditiKraft.Krafter.Contracts.Contracts.Auth;

public sealed record LoginResponse(TokenResponse? Tokens, TwoFactorChallengeResponse? Challenge)
{
    public bool IsCompleted => Tokens is not null && Challenge is null;
    public bool RequiresTwoFactor => Tokens is null && Challenge is not null;
    public static LoginResponse Completed(TokenResponse tokens) => new(tokens, null);
    public static LoginResponse Pending(string id, DateTimeOffset expires) => new(null, new(id, expires));
}

public sealed record TwoFactorChallengeResponse(string Id, DateTimeOffset ExpiresAt);
