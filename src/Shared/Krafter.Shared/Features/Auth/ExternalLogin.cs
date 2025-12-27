namespace Krafter.Shared.Features.Auth;

public sealed class ExternalAuth
{
    public sealed class GoogleAuthRequest
    {
        public string Code { get; set; } = string.Empty;
    }
}
