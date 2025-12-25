using Krafter.Api.Client.Models;

namespace Krafter.UI.Web.Client.Features.Auth._Shared;

public interface IAuthenticationService
{
    public event Action<string?>? LoginChange;

    public Task<bool> LoginAsync(TokenRequestInput model);

    public Task LogoutAsync(string methodName);

    public Task<bool> RefreshAsync();
}
