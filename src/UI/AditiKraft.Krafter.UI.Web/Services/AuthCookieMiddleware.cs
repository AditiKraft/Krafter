using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.UI.Web.Services;

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
        $"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}",
        $"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}/{RouteSegment.Refresh}",
        $"/{KrafterRoute.ApiPrefix}/{KrafterRoute.ExternalAuth}/{RouteSegment.Google}"
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
                    Response<TokenResponse>? tokenResponse =
                        await System.Text.Json.JsonSerializer.DeserializeAsync<Response<TokenResponse>>(buffer, JsonOptions);

                    if (tokenResponse is { Data: not null, IsError: false })
                    {
                        IKrafterLocalStorageService localStorage =
                            context.RequestServices.GetRequiredService<IKrafterLocalStorageService>();
                        await localStorage.CacheAuthTokens(tokenResponse.Data);
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
