using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.UI.Web.Infrastructure.Auth;

/// <summary>
/// Middleware that intercepts backend auth endpoint responses (login, refresh, external-auth)
/// and persists tokens as HttpOnly cookies + HybridCache for server-side rendering.
/// <para>
/// Used in both hosting modes:
/// <list type="bullet">
/// <item>Split-host — intercepts BFF proxy endpoint responses after Refit returns from the backend.</item>
/// <item>Single-host — intercepts backend route responses directly (no BFF proxy needed).</item>
/// </list>
/// </para>
/// </summary>
public class AuthCookieMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> AuthPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        $"/{ApiRoutes.ApiPrefix}/{ApiRoutes.TwoFactor}/login",
        $"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}",
        $"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}/{RouteSegment.Refresh}",
        $"/{ApiRoutes.ApiPrefix}/{ApiRoutes.ExternalAuth}/{RouteSegment.Google}"
    };

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsAuthTokenEndpoint(context.Request))
        {
            await next(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";

        // Buffer the response body so we can read the token data before it's sent to the client
        Stream originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);

            buffer.Position = 0;

            // Only process successful responses
            if (context.Response.StatusCode is >= 200 and < 300)
            {
                try
                {
                    bool primary = context.Request.Path.Equals($"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}", StringComparison.OrdinalIgnoreCase) ||
                        context.Request.Path.Equals($"/{ApiRoutes.ApiPrefix}/{ApiRoutes.ExternalAuth}/{RouteSegment.Google}", StringComparison.OrdinalIgnoreCase);
                    TokenResponse? completed;
                    if (primary)
                    {
                        Response<LoginResponse>? login = await System.Text.Json.JsonSerializer.DeserializeAsync<Response<LoginResponse>>(buffer, JsonOptions);
                        completed = login is { IsError: false, Data.Challenge: null } ? login.Data?.Tokens : null;
                    }
                    else
                    {
                        Response<TokenResponse>? response = await System.Text.Json.JsonSerializer.DeserializeAsync<Response<TokenResponse>>(buffer, JsonOptions);
                        completed = response is { IsError: false } ? response.Data : null;
                    }
                    if (completed is not null && !string.IsNullOrWhiteSpace(completed.Token) && !string.IsNullOrWhiteSpace(completed.RefreshToken))
                    {
                        IAuthStorageService storage = context.RequestServices.GetRequiredService<IAuthStorageService>();
                        await storage.CacheAuthTokens(completed);
                    }
                }
                catch (Exception ex)
                {
                    ILogger<AuthCookieMiddleware> logger =
                        context.RequestServices.GetRequiredService<ILogger<AuthCookieMiddleware>>();
                    logger.LogWarning(ex, "Failed to extract token response for cookie persistence");
                }
            }
        }
        finally
        {
            // Always restore the original stream and copy buffered content back
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
            context.Response.Body = originalBody;
        }
    }

    private static bool IsAuthTokenEndpoint(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
        {
            return false;
        }

        return AuthPaths.Contains(request.Path.Value ?? string.Empty);
    }
}
