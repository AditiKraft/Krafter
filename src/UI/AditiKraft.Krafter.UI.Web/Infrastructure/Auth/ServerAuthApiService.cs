using System.Net;
using System.Security.Cryptography;
using System.Text;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using Microsoft.Extensions.Caching.Hybrid;
using Refit;

namespace AditiKraft.Krafter.UI.Web.Infrastructure.Auth;

public class ServerAuthApiService(
    IAuthApi authApi,
    IAuthStorageService localStorage,
    HybridCache cache,
    ILogger<ServerAuthApiService> logger) : IAuthApiService
{
    private static readonly HybridCacheEntryOptions RefreshCacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(5),
        LocalCacheExpiration = TimeSpan.FromSeconds(5),
        Flags = HybridCacheEntryFlags.DisableDistributedCache
    };

    public async Task<Response<TokenResponse>> CreateTokenAsync(TokenRequest request,
        CancellationToken cancellation)
    {
        try
        {
            return await authApi.CreateTokenAsync(request, cancellation);
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
            // Handler scopes and HTTP requests share the result only for this exact token pair.
            // Hash credentials to keep them out of cache keys; never write tokens to the distributed cache.
            string key = "auth-refresh:" + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes($"{request.Token}\0{request.RefreshToken}")));
            return await cache.GetOrCreateAsync(key, async cancel =>
            {
                Response<TokenResponse> response = await authApi.RefreshTokenAsync(request, cancel);
                if (response is not { IsError: false, Data: not null } ||
                    string.IsNullOrWhiteSpace(response.Data.Token) ||
                    string.IsNullOrWhiteSpace(response.Data.RefreshToken))
                {
                    // A failed attempt must not prevent a later retry with the same credentials.
                    throw new RefreshRejectedException(response);
                }

                return response;
            }, RefreshCacheOptions, cancellationToken: cancellation);
        }
        catch (RefreshRejectedException ex)
        {
            return ex.Response;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
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
            return await authApi.GoogleAuthAsync(googleRequest, cancellation);
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

    private sealed class RefreshRejectedException(Response<TokenResponse> response) : Exception
    {
        public Response<TokenResponse> Response { get; } = response;
    }
}
