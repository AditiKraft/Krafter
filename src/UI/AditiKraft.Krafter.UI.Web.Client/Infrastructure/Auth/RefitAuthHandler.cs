using System.Net;
using System.Net.Http.Headers;
using AditiKraft.Krafter.Contracts.Common;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public class RefitAuthHandler(
    IAuthStorageService localStorage,
    IAuthenticationService authenticationService,
    ILogger<RefitAuthHandler> logger) : DelegatingHandler
{
    // Public endpoints that must NOT trigger a refresh or require an auth token
    private static readonly string[] PublicPaths =
    [
        $"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}",
        $"/{ApiRoutes.ApiPrefix}/{ApiRoutes.ExternalAuth}",
        $"/{ApiRoutes.ApiPrefix}/{ApiRoutes.AppInfo}",
        $"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}/{RouteSegment.Refresh}",
        $"/{ApiRoutes.ApiPrefix}/{ApiRoutes.ExternalAuth}/{RouteSegment.Google}",
        $"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}/{RouteSegment.Logout}",
        $"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tenants}/{RouteSegment.SeedData}",
        "/login"
    ];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string path = request.RequestUri?.AbsolutePath ?? request.RequestUri?.OriginalString ?? string.Empty;
        bool isPublicPath = IsPublicPath(path);

        // Get current token
        string? accessToken = await localStorage.GetCachedAuthTokenAsync();

        if (!isPublicPath && !string.IsNullOrEmpty(accessToken) && AuthTokenService.NeedsRefresh(accessToken))
        {
            try
            {
                await authenticationService.RefreshAsync();
                accessToken = await localStorage.GetCachedAuthTokenAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Token refresh failed before request");
            }
        }

        // Inject token if available
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        // Send request
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        // Handle 401 for non-public paths - attempt refresh and retry once
        if (response.StatusCode == HttpStatusCode.Unauthorized && !isPublicPath)
        {
            string? rejectedToken = accessToken;
            try
            {
                if (await authenticationService.RefreshAsync())
                {
                    accessToken = await localStorage.GetCachedAuthTokenAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Token refresh failed after 401");
            }

            // Retry only when another call or this refresh produced a different token.
            if (!string.IsNullOrEmpty(accessToken) && accessToken != rejectedToken)
            {
                using HttpRequestMessage retryRequest = await CloneRequestAsync(request);
                retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                response.Dispose();
                response = await base.SendAsync(retryRequest, cancellationToken);
            }
        }

        return response;
    }

    private static bool IsPublicPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        string normalized = path.Trim().ToLowerInvariant();
        return PublicPaths.Any(p =>
            normalized.StartsWith(p, StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(p.Trim('/')));
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };

        if (request.Content != null)
        {
            byte[] content = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (KeyValuePair<string, object?> option in request.Options)
        {
            clone.Options.TryAdd(option.Key, option.Value);
        }

        return clone;
    }
}
