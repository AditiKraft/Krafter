namespace AditiKraft.Krafter.Contracts.Contracts.Auth;

public class GoogleAuthRequest
{
    // A backend-created handoff, never a provider authorization code.
    public string Handoff { get; set; } = string.Empty;
    public string BrowserNonce { get; set; } = string.Empty;
}
