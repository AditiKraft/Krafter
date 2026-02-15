using System.Net;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Api;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using Refit;

namespace AditiKraft.Krafter.UI.Web.Services;

public class ServerSideApiService(
    IAuthApi authApi,
    IKrafterLocalStorageService localStorage,
    ILogger<ServerSideApiService> logger) : IApiService
{
    public async Task<Response<TokenResponse>> CreateTokenAsync(TokenRequest request,
        CancellationToken cancellation)
    {
        try
        {
            Response<TokenResponse> tokenResponse = await authApi.CreateTokenAsync(request, cancellation);

            if (tokenResponse.Data != null && !tokenResponse.IsError)
            {
                await localStorage.CacheAuthTokens(tokenResponse.Data);
            }

            return tokenResponse;
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "Error during server-side token creation");
            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "Failed to create token. Please log in again.",
                StatusCode = (int)ex.StatusCode
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during server-side token creation");
            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "Failed to create token. Please log in again.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            };
        }
    }

    public async Task<Response<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request,
        CancellationToken cancellation)
    {
        try
        {
            Response<TokenResponse> tokenResponse = await authApi.RefreshTokenAsync(request, cancellation);

            if (tokenResponse.Data != null && !tokenResponse.IsError)
            {
                await localStorage.CacheAuthTokens(tokenResponse.Data);
            }

            return tokenResponse;
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "Error during server-side token refresh");
            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "Failed to refresh token. Please log in again.",
                StatusCode = (int)ex.StatusCode
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during server-side token refresh");
            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "Failed to refresh token. Please log in again.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            };
        }
    }

    public async Task<Response<TokenResponse>> ExternalAuthAsync(TokenRequest request,
        CancellationToken cancellation)
    {
        try
        {
            var googleRequest = new GoogleAuthRequest { Code = request.Code ?? string.Empty };
            Response<TokenResponse> tokenResponse = await authApi.GoogleAuthAsync(googleRequest, cancellation);

            if (tokenResponse.Data != null && !tokenResponse.IsError)
            {
                await localStorage.CacheAuthTokens(tokenResponse.Data);
            }

            return tokenResponse;
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "Error during server-side external auth");
            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "Failed to authenticate. Please try again.",
                StatusCode = (int)ex.StatusCode
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during server-side external auth");
            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "Failed to authenticate. Please try again.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            };
        }
    }

    public async Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellation)
    {
        try
        {
            string? token = await localStorage.GetCachedAuthTokenAsync();
            string? refreshToken = await localStorage.GetCachedRefreshTokenAsync();
            DateTime authTokenExpiryDate = await localStorage.GetAuthTokenExpiryDate();
            DateTime refreshTokenExpiry = await localStorage.GetRefreshTokenExpiryDate();
            ICollection<string>? permissions = await localStorage.GetCachedPermissionsAsync();

            if (!string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(refreshToken))
            {
                return new Response<TokenResponse>
                {
                    Data = new TokenResponse(
                        token,
                        refreshToken,
                        refreshTokenExpiry,
                        authTokenExpiryDate,
                        permissions?.ToList() ?? []),
                    StatusCode = (int)HttpStatusCode.OK
                };
            }

            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "No valid token found. Please log in again.",
                StatusCode = (int)HttpStatusCode.Unauthorized
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting current token");
            return new Response<TokenResponse>
            {
                IsError = true,
                Message = "Failed to get current token.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            };
        }
    }

    public Task LogoutAsync(CancellationToken cancellation) => localStorage.ClearCacheAsync();
}
