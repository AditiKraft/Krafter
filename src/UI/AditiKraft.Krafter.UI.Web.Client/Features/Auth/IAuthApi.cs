using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Auth;

public interface IAuthApi
{
    [Post("/api/two-factor/login")]
    [Headers("X-Krafter-Mfa: 1")]
    Task<Response<TokenResponse>> CompleteTwoFactorAsync([Body] TwoFactorRequest request, CancellationToken cancellationToken = default);

    [Get("/api/tokens/validate")]
    Task<Response<bool>> ValidateSessionAsync([Header("Authorization")] string authorization, CancellationToken cancellationToken = default);

    [Post("/api/tokens")]
    public Task<Response<LoginResponse>> CreateTokenAsync([Body] TokenRequest request,
        CancellationToken cancellationToken = default);

    [Post("/api/tokens/refresh")]
    public Task<Response<TokenResponse>> RefreshTokenAsync([Body] RefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    [Post("/api/external-auth/google")]
    public Task<Response<LoginResponse>> GoogleAuthAsync([Body] GoogleAuthRequest request,
        CancellationToken cancellationToken = default);

    [Post("/api/tokens/logout")]
    public Task LogoutAsync(CancellationToken cancellationToken = default);

    [Get("/api/tokens/current")]
    public Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellationToken = default);
}
