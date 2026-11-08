using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public interface IAuthenticationService
{
    public event Action<string?>? LoginChange;
    TwoFactorChallengeResponse? PendingChallenge => null;
    string? ErrorMessage => null;
    Task<bool> VerifyTwoFactorAsync(string code, bool recovery) => Task.FromResult(false);
    void CancelTwoFactor() { }


    public Task<bool> LoginAsync(TokenRequest model);

    public Task LogoutAsync(string methodName);

    public Task<bool> RefreshAsync();
}
