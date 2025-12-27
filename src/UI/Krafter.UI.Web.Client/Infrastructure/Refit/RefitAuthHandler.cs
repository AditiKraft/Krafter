using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Krafter.UI.Web.Client.Features.Auth._Shared;
using Krafter.UI.Web.Client.Infrastructure.Storage;

namespace Krafter.UI.Web.Client.Infrastructure.Refit;

/// <summary>
/// DelegatingHandler that injects Bearer token and handles token refresh for Refit clients.
/// </summary>
public class RefitAuthHandler(
    IKrafterLocalStorageService localStorage,
    IAuthenticationService authenticationService,
    ILogger<RefitAuthHandler> logger) : DelegatingHandler
{
    // Public endpoints that must NOT trigger a refresh or require an auth token
    private static readonly string[] PublicPaths =
    [
        "/tokens/refresh", "/tokens/create", "/tokens/current", "/tokens/logout",
        "/external-auth", "/external-auth/google", "/app-info", "/seed", "/login"
    ];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string path = request.RequestUri?.AbsolutePath ?? request.RequestUri?.OriginalString ?? string.Empty;
        bool isPublicPath = IsPublicPath(path);

        // Get current token
        string? accessToken = await localStorage.GetCachedAuthTokenAsync();

        // For non-public paths, check if token needs refresh
        if (!isPublicPath && !string.IsNullOrEmpty(accessToken) && IsTokenExpired(accessToken))
        {
            logger.LogInformation("Token expired, attempting refresh before request to {Path}", path);
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
            logger.LogInformation("Received 401, attempting token refresh and retry for {Path}", path);
            try
            {
                bool refreshed = await authenticationService.RefreshAsync();
                if (refreshed)
                {
                    accessToken = await localStorage.GetCachedAuthTokenAsync();
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        // Clone request and retry
                        var retryRequest = await CloneRequestAsync(request);
                        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                        response.Dispose();
                        response = await base.SendAsync(retryRequest, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Token refresh failed after 401");
            }
        }

        return response;
    }

    private static bool IsPublicPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string normalized = path.Trim().ToLowerInvariant();
        return PublicPaths.Any(p =>
            normalized.StartsWith(p, StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(p.Trim('/')));
    }

    private static bool IsTokenExpired(string token)
    {
        try
        {
            string[] parts = token.Split('.');
            if (parts.Length != 3) return true;

            byte[] payload = Base64UrlDecode(parts[1]);
            using var doc = JsonDocument.Parse(payload);
            long exp = doc.RootElement.GetProperty("exp").GetInt64();
            var expiry = DateTimeOffset.FromUnixTimeSeconds(exp);
            return DateTimeOffset.UtcNow >= expiry.AddMinutes(-1);
        }
        catch
        {
            return true;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        string padded = input.Length % 4 == 0 ? input : input + new string('=', 4 - (input.Length % 4));
        string base64 = padded.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        if (request.Content != null)
        {
            byte[] content = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);

            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in request.Options)
        {
            clone.Options.TryAdd(option.Key, option.Value);
        }

        return clone;
    }
}
