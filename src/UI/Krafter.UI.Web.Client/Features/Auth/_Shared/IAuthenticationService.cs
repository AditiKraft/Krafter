using Krafter.Shared.Contracts.Auth;

namespace Krafter.UI.Web.Client.Features.Auth._Shared;

public interface IAuthenticationService
{
    public event Action<string?>? LoginChange;

    public Task<bool> LoginAsync(TokenRequest model);

    public Task LogoutAsync(string methodName);

    public Task<bool> RefreshAsync();
}
