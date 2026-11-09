using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public interface IAuthenticationService
{
    public event Action<string?>? LoginChange;
    TwoFactorChallengeResponse? PendingChallenge { get; }
    string? ErrorMessage { get; }
    Task<bool> VerifyTwoFactorAsync(string code, bool recovery);
    void CancelTwoFactor();


    public Task<bool> LoginAsync(TokenRequest model);

    // A supplied version makes automatic cleanup conditional; false can mean a newer flow was preserved.
    public Task<bool> LogoutAsync(string methodName, long? expectedSessionVersion = null);

    public Task<bool> RefreshAsync();
}
