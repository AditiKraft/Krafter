using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client.Common.Constants;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Infrastructure.Api;
using Krafter.UI.Web.Client.Infrastructure.Storage;

namespace Krafter.UI.Web.Services
{
    public class ServerSideApiService(

       // KrafterClient krafterClient,
     IHttpClientFactory httpClientFactory,
     IKrafterLocalStorageService localStorage,
     ILogger<ServerSideApiService> logger) : IApiService
    {
        public async Task<Response<TokenResponse>> CreateTokenAsync(TokenRequestInput request, CancellationToken cancellation)
        {
            try
            {

               //var res= await krafterClient.Tokens.Create.PostAsync(request);

                var response = await httpClientFactory
                    .CreateClient("KrafterUIAPI")
                    .PostAsync($"{KrafterRoute.Tokens}/create", JsonContent.Create(request));

                var tokenResponse = await response.Content.ReadFromJsonAsync<Response<TokenResponse>>();

                if (tokenResponse?.Data != null && !tokenResponse.IsError)
                {
                    await localStorage.CacheAuthTokens(tokenResponse.Data);
                }
                if (tokenResponse == null)
                {
                    return new Response<TokenResponse>
                    {
                        IsError = true,
                        Message = "Failed to create token. Please log in again.",
                        StatusCode = StatusCodes.Status500InternalServerError
                    };
                }

                return tokenResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during server-side token creation");
                return new Response<TokenResponse>
                {
                    IsError = true,
                    Message = "Failed to create token. Please log in again.",
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }

        public async Task<Response<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellation)
        {
            try
            {
                var response = await httpClientFactory
                    .CreateClient("KrafterUIAPI")
                    .PostAsync($"{KrafterRoute.Tokens}/refresh", JsonContent.Create(request));

                var tokenResponse = await response.Content.ReadFromJsonAsync<Response<TokenResponse>>();

                if (tokenResponse?.Data != null && !tokenResponse.IsError)
                {
                    await localStorage.CacheAuthTokens(tokenResponse.Data);
                }
                if (tokenResponse == null)
                {
                    return new Response<TokenResponse>
                    {
                        IsError = true,
                        Message = "Failed to refresh token. Please log in again.",
                        StatusCode = StatusCodes.Status500InternalServerError
                    };
                }

                return tokenResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during server-side token refresh");
                return new Response<TokenResponse>
                {
                    IsError = true,
                    Message = "Failed to refresh token. Please log in again.",
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }

        public async Task<Response<TokenResponse>> ExternalAuthAsync(TokenRequestInput request, CancellationToken cancellation)
        {
            try
            {
                var response = await httpClientFactory
                    .CreateClient("KrafterUIAPI")
                    .PostAsync($"{KrafterRoute.ExternalAuth}/google", JsonContent.Create(request));

                var tokenResponse = await response.Content.ReadFromJsonAsync<Response<TokenResponse>>();

                if (tokenResponse?.Data != null && !tokenResponse.IsError)
                {
                    await localStorage.CacheAuthTokens(tokenResponse.Data);
                }
                if (tokenResponse == null)
                {
                    return new Response<TokenResponse>
                    {
                        IsError = true,
                        Message = "Failed to authenticate. Please try again.",
                        StatusCode = StatusCodes.Status500InternalServerError
                    };
                }

                return tokenResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during server-side external auth");
                return new Response<TokenResponse>
                {
                    IsError = true,
                    Message = "Failed to authenticate. Please try again.",
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }

        public async Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellation)
        {
            try
            {
                var token = await localStorage.GetCachedAuthTokenAsync();
                var refreshToken = await localStorage.GetCachedRefreshTokenAsync();
                var authTokenExpiryDate = await localStorage.GetAuthTokenExpiryDate();
                var refreshTokenExpiry = await localStorage.GetRefreshTokenExpiryDate();
                var permissions = await localStorage.GetCachedPermissionsAsync();

                if (!string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(refreshToken))
                {
                    return new Response<TokenResponse>
                    {
                        Data = new TokenResponse()
                        {
                            RefreshToken = refreshToken,
                            Token = token,
                            RefreshTokenExpiryTime = refreshTokenExpiry,
                            TokenExpiryTime = authTokenExpiryDate,
                            Permissions = permissions?.ToList()
                        },
                        StatusCode = StatusCodes.Status200OK
                    };
                }

                return new Response<TokenResponse>
                {
                    IsError = true,
                    Message = "No valid token found. Please log in again.",
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting current token");
                return new Response<TokenResponse>
                {
                    IsError = true,
                    Message = "Failed to get current token.",
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }

        public async Task LogoutAsync(CancellationToken cancellation)
        {
            try
            {
                await localStorage.ClearCacheAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during server-side logout");
            }
        }
    }
}