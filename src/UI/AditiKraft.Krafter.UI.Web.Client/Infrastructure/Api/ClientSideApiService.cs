using System.Net;
using AditiKraft.Krafter.Shared.Common.Models;
using AditiKraft.Krafter.Shared.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Storage;
using Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Api;

public class ClientSideApiService(
    IAuthApi authApi,
    IKrafterLocalStorageService localStorage,
    ILogger<ClientSideApiService> logger) : IApiService
{
    public async Task<Response<TokenResponse>> CreateTokenAsync(TokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            Response<TokenResponse> response = await authApi.CreateTokenAsync(request, cancellationToken);
            return response;
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "Error during client-side token creation");
            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "Failed to create token. Please log in again.",
                StatusCode = (int)ex.StatusCode
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during client-side token creation");
            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "Failed to create token. Please log in again.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            };
        }
    }

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

    public async Task<Response<TokenResponse>> ExternalAuthAsync(TokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var googleRequest = new GoogleAuthRequest { Code = request.Code ?? string.Empty };
            Response<TokenResponse> response = await authApi.GoogleAuthAsync(googleRequest, cancellationToken);
            return response;
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "Error during client-side external auth");
            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "Failed to authenticate. Please try again.",
                StatusCode = (int)ex.StatusCode
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during client-side external auth");
            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "Failed to authenticate. Please try again.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            };
        }
    }

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
