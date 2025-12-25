namespace Backend.Features.Auth.Token;

public record TokenResponse(
    string Token,
    string RefreshToken,
    DateTime RefreshTokenExpiryTime,
    DateTime TokenExpiryTime,
    List<string> Permissions);
