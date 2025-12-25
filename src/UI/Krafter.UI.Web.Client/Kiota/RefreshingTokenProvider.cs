using Krafter.UI.Web.Client.Infrastructure.Storage;
using Microsoft.Kiota.Abstractions.Authentication;
using System.Text.Json;
using Krafter.UI.Web.Client.Features.Auth._Shared;

namespace Krafter.UI.Web.Client.Kiota;

public class RefreshingTokenProvider : IAccessTokenProvider
{
    private readonly IKrafterLocalStorageService _localStorage;
    private readonly IAuthenticationService _http;

    public RefreshingTokenProvider(IKrafterLocalStorageService localStorage, IAuthenticationService http)
    {
        _localStorage = localStorage;
        _http = http;
    }

    public AllowedHostsValidator AllowedHostsValidator { get; } = new();

    public async Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        // Public endpoints that must NOT trigger a refresh or require an auth token
        // Keep this list in sync with server-side public routes (/tokens, /external-auth, /app-info, /seed, /login, etc.)
        string[] publicPaths = new[]
        {
            "/tokens/refresh", "/tokens/create", "/tokens/current", "/tokens/logout", "/external-auth",
            "/external-auth/google", "/app-info", "/seed", "/login"
        };

        // If the request is for a public path, return current token (possibly null) and DO NOT attempt refresh
        string path = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;
        if (!string.IsNullOrEmpty(path))
        {
            string normalized = path.Trim().ToLowerInvariant();
            if (publicPaths.Any(p =>
                    normalized.StartsWith(p, StringComparison.OrdinalIgnoreCase) || normalized.Contains(p.Trim('/'))))
            {
                return await _localStorage.GetCachedAuthTokenAsync() ?? string.Empty;
            }
        }

        string? accessToken = await _localStorage.GetCachedAuthTokenAsync();
        if (string.IsNullOrEmpty(accessToken) || IsExpired(accessToken))
        {
            // Only attempt refresh for non-public endpoints
            try
            {
                await _http.RefreshAsync();
            }
            catch
            {
                // swallow here — RefreshAsync will handle logout if needed
            }

            accessToken = await _localStorage.GetCachedAuthTokenAsync();
        }

        return accessToken ?? string.Empty;
    }

    private bool IsExpired(string token)
    {
        try
        {
            string[] parts = token.Split('.');
            var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            long exp = payload.RootElement.GetProperty("exp").GetInt64();
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
}
