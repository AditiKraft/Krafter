namespace Krafter.Shared.Contracts.Auth;

/// <summary>
/// Response model containing authentication tokens.
/// </summary>
public record TokenResponse(
    string Token,
    string RefreshToken,
    DateTime RefreshTokenExpiryTime,
    DateTime TokenExpiryTime,
    List<string> Permissions);
