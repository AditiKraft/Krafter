namespace Krafter.Shared.Features.Auth._Shared;

public record TokenResponse(
    string Token,
    string RefreshToken,
    DateTime RefreshTokenExpiryTime,
    DateTime TokenExpiryTime,
    List<string> Permissions);
