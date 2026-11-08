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

    public Task<bool> LogoutAsync(string methodName);

    public Task<bool> RefreshAsync();
}
