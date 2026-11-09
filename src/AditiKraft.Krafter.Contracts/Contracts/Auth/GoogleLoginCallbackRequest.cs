namespace AditiKraft.Krafter.Contracts.Contracts.Auth;

public sealed class GoogleLoginCallbackRequest
{
    public string State { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string RootCorrelation { get; set; } = string.Empty;
}
