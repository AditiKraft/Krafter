namespace AditiKraft.Krafter.Contracts.Contracts.Auth;

public sealed record GoogleLoginStartResponse(string State, string AuthorizationUrl, DateTimeOffset ExpiresAt);
