using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

public interface IAuthApi
{
    [Post("/tokens")]
    public Task<Response<TokenResponse>> CreateTokenAsync([Body] TokenRequest request,
        CancellationToken cancellationToken = default);

    [Post("/tokens/refresh")]
    public Task<Response<TokenResponse>> RefreshTokenAsync([Body] RefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    [Post("/external-auth/google")]
    public Task<Response<TokenResponse>> GoogleAuthAsync([Body] GoogleAuthRequest request,
        CancellationToken cancellationToken = default);

    [Post("/tokens/logout")]
    public Task LogoutAsync(CancellationToken cancellationToken = default);

    [Get("/tokens/current")]
    public Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellationToken = default);
}
