using AditiKraft.Krafter.Shared.Contracts.Auth;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Api;

public interface IApiService
{
    public Task<Response<TokenResponse>> CreateTokenAsync(TokenRequest request, CancellationToken cancellation);

    public Task<Response<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellation);

    public Task<Response<TokenResponse>> ExternalAuthAsync(TokenRequest request, CancellationToken cancellation);

    public Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellation);

    public Task LogoutAsync(CancellationToken cancellation);
}
