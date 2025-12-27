using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Auth;
using Refit;

namespace Krafter.UI.Web.Client.Infrastructure.Refit;

/// <summary>
/// Refit interface for authentication endpoints.
/// </summary>
public interface IAuthApi
{
    [Post("/tokens/create")]
    Task<Response<TokenResponse>> CreateTokenAsync([Body] TokenRequest request, CancellationToken cancellationToken = default);

    [Post("/tokens/refresh")]
    Task<Response<TokenResponse>> RefreshTokenAsync([Body] RefreshTokenRequest request, CancellationToken cancellationToken = default);

    [Post("/external-auth/google")]
    Task<Response<TokenResponse>> GoogleAuthAsync([Body] GoogleAuthRequest request, CancellationToken cancellationToken = default);
}
