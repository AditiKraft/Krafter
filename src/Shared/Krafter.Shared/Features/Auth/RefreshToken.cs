namespace Krafter.Shared.Features.Auth;

public sealed class RefreshToken
{
    public record RefreshTokenRequest(string Token, string RefreshToken);
}
