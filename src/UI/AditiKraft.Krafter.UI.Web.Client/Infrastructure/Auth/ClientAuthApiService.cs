using System.Net;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public class ClientAuthApiService(
    IAuthApi authApi,
    IAuthStorageService localStorage,
    ILogger<ClientAuthApiService> logger) : IAuthApiService
{
    public Task<Response<TokenResponse>> CompleteTwoFactorAsync(TwoFactorRequest request, CancellationToken cancellation) =>
        AuthApiErrors.CallAsync(() => authApi.CompleteTwoFactorAsync(request, cancellation), cancellation);

    public Task<Response<LoginResponse>> CreateTokenAsync(TokenRequest request, CancellationToken cancellation) =>
        AuthApiErrors.CallAsync(() => authApi.CreateTokenAsync(request, cancellation), cancellation);

    public async Task<Response<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            Response<TokenResponse> response = await authApi.RefreshTokenAsync(request, cancellationToken);
            return response;
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "Error during client-side token refresh");
            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "Failed to refresh token. Please log in again.",
                StatusCode = (int)ex.StatusCode
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during client-side token refresh");
            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "Failed to refresh token. Please log in again.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            };
        }
    }

    public Task<Response<LoginResponse>> ExternalAuthAsync(TokenRequest request, CancellationToken cancellation) =>
        AuthApiErrors.CallAsync(() => authApi.GoogleAuthAsync(new GoogleAuthRequest
        { Handoff = request.GoogleHandoff ?? string.Empty, BrowserNonce = request.GoogleBrowserNonce ?? string.Empty }, cancellation), cancellation);

    public async Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            Response<TokenResponse> response = await authApi.GetCurrentTokenAsync(cancellationToken);
            return response;
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "Error during client-side token retrieval");
            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "Failed to retrieve token. Please log in again.",
                StatusCode = (int)ex.StatusCode
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during client-side token retrieval");
            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "Failed to retrieve token. Please log in again.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            };
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Call BFF to clear HttpOnly cookies on the server
            await authApi.LogoutAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling logout endpoint");
        }

        // Clear local storage regardless of BFF call result
        await localStorage.ClearCacheAsync();
    }
}
