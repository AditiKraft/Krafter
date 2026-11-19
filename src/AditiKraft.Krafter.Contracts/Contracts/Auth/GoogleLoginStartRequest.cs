namespace AditiKraft.Krafter.Contracts.Contracts.Auth;

public sealed class GoogleLoginStartRequest
{
    public string UiOrigin { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "/";
    public string ClientNonceHash { get; set; } = string.Empty;
    public string RootCorrelationHash { get; set; } = string.Empty;
}
