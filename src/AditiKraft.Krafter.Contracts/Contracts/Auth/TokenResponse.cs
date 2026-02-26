namespace AditiKraft.Krafter.Contracts.Contracts.Auth;

public record TokenResponse(
    string Token,
    string RefreshToken,
    DateTime RefreshTokenExpiryTime,
    DateTime TokenExpiryTime,
    List<string> Permissions);
