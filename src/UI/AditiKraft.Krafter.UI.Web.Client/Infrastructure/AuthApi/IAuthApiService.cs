using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.AuthApi;

public interface IAuthApiService
{
    public Task<Response<TokenResponse>> CreateTokenAsync(TokenRequest request, CancellationToken cancellation);

    public Task<Response<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellation);

    public Task<Response<TokenResponse>> ExternalAuthAsync(TokenRequest request, CancellationToken cancellation);

    public Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellation);

    public Task LogoutAsync(CancellationToken cancellation);
}

