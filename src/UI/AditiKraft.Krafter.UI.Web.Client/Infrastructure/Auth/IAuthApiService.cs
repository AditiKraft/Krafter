using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public interface IAuthApiService
{
    Task<Response<TokenResponse>> CompleteTwoFactorAsync(TwoFactorRequest request, CancellationToken cancellation) =>
        Task.FromResult(Response<TokenResponse>.BadRequest("Two-factor login is unavailable."));

    public Task<Response<LoginResponse>> CreateTokenAsync(TokenRequest request, CancellationToken cancellation);

    public Task<Response<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellation);

    public Task<Response<LoginResponse>> ExternalAuthAsync(TokenRequest request, CancellationToken cancellation);

    public Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellation);

    public Task LogoutAsync(CancellationToken cancellation);
}
