using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Auth;

namespace Krafter.UI.Web.Client.Infrastructure.Api;

public interface IApiService
{
    Task<Response<TokenResponse>> CreateTokenAsync(TokenRequest request, CancellationToken cancellation);

    Task<Response<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellation);

    Task<Response<TokenResponse>> ExternalAuthAsync(TokenRequest request, CancellationToken cancellation);

    Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellation);

    Task LogoutAsync(CancellationToken cancellation);
}
