namespace Krafter.Shared.Contracts.Auth;

/// <summary>
/// Request model for refreshing an authentication token.
/// </summary>
public class RefreshTokenRequest
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
