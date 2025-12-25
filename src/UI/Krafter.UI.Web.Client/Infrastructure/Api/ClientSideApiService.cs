using System.Net;
using System.Net.Http.Json;
using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client.Common.Constants;
using Krafter.UI.Web.Client.Common.Models;

namespace Krafter.UI.Web.Client.Infrastructure.Api;

public class ClientSideApiService(
    IHttpClientFactory httpClientFactory,
    ILogger<ClientSideApiService> logger) : IApiService
{
    public async Task<Response<TokenResponse>> CreateTokenAsync(TokenRequestInput request,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage response = await httpClientFactory
                .CreateClient("KrafterUIBFF")
                .PostAsync($"{KrafterRoute.Tokens}/create", JsonContent.Create(request));

            Response<TokenResponse>? res = await response.Content.ReadFromJsonAsync<Response<TokenResponse>>();

            if (res == null)
            {
                return new Response<TokenResponse>
                {
                    IsError = true,
                    Message = "Failed to create token. Please log in again.",
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }

            return res;
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
            HttpResponseMessage response = await httpClientFactory
                .CreateClient("KrafterUIBFF")
                .PostAsync($"{KrafterRoute.Tokens}/refresh", JsonContent.Create(request));

            Response<TokenResponse>? res = await response.Content.ReadFromJsonAsync<Response<TokenResponse>>();
            if (res == null)
            {
                return new Response<TokenResponse>
                {
                    IsError = true,
                    Message = "Failed to refresh token. Please log in again.",
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }

            return res;
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

    public async Task<Response<TokenResponse>> ExternalAuthAsync(TokenRequestInput request,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage response = await httpClientFactory
                .CreateClient("KrafterUIBFF")
                .PostAsync($"{KrafterRoute.ExternalAuth}/google", JsonContent.Create(request));

            Response<TokenResponse>? res = await response.Content.ReadFromJsonAsync<Response<TokenResponse>>();
            if (res == null)
            {
                return new Response<TokenResponse>
                {
                    IsError = true,
                    Message = "Failed to authenticate. Please try again.",
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }

            return res;
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

    public async Task<Response<List<string>>> GetUserPermissionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage response = await httpClientFactory
                .CreateClient("KrafterUIBFF")
                .GetAsync($"/{KrafterRoute.Users}/permissions");

            Response<List<string>>? res = await response.Content.ReadFromJsonAsync<Response<List<string>>>();
            if (res == null)
            {
                return new Response<List<string>>
                {
                    IsError = true,
                    Message = "Failed to retrieve permissions. Please log in again.",
                    Data = new List<string>(),
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }

            return res;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during client-side permissions retrieval");
            return new Response<List<string>>
            {
                IsError = true,
                Message = "Failed to retrieve permissions. Please log in again.",
                Data = new List<string>(),
                StatusCode = (int)HttpStatusCode.InternalServerError
            };
        }
    }

    public async Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage response = await httpClientFactory
                .CreateClient("KrafterUIBFF")
                .GetAsync($"{KrafterRoute.Tokens}/current");

            Response<TokenResponse>? res = await response.Content.ReadFromJsonAsync<Response<TokenResponse>>();
            if (res == null)
            {
                return new Response<TokenResponse>
                {
                    IsError = true,
                    Message = "Failed to get current token.",
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }

            return res;
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

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            await httpClientFactory
                .CreateClient("KrafterUIBFF")
                .PostAsync($"{KrafterRoute.Tokens}/logout", null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during client-side logout");
        }
    }
}
