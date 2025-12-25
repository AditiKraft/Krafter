using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client.Common.Models;

namespace Krafter.UI.Web.Client.Infrastructure.Api;

public interface IApiService
{
    public Task<Response<TokenResponse>> CreateTokenAsync(TokenRequestInput request, CancellationToken cancellation);

    public Task<Response<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellation);

    public Task<Response<TokenResponse>> ExternalAuthAsync(TokenRequestInput request, CancellationToken cancellation);

    public Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellation);

    public Task LogoutAsync(CancellationToken cancellation);
}
