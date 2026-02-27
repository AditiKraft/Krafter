using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Auth.Common;

public interface IAuthenticationService
{
    public event Action<string?>? LoginChange;

    public Task<bool> LoginAsync(TokenRequest model);

    public Task LogoutAsync(string methodName);

    public Task<bool> RefreshAsync();
}


