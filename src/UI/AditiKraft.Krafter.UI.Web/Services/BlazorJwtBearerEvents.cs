using System.Security.Claims;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Common.Constants;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.AuthApi;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.IdentityModel.Tokens.Jwt;

namespace AditiKraft.Krafter.UI.Web.Services;

/// <summary>
/// Determines how the Blazor host is deployed, affecting JWT challenge/forbidden behavior.
/// </summary>
public enum BlazorHostingMode
{
    /// <summary>
    /// UI runs as a separate process. OnChallenge always redirects to login.
    /// </summary>
    SplitHost,

    /// <summary>
    /// UI and backend API share a process. OnChallenge returns 401 JSON for /api/* requests,
    /// redirects to login for browser requests.
    /// </summary>
    SingleHost
}

/// <summary>
/// Unified JWT bearer events for Blazor server-side rendering.
/// Handles cookie-based token reading, proactive token refresh, permission claim enrichment,
/// and hosting-mode-aware challenge/forbidden responses.
/// </summary>
public class BlazorJwtBearerEvents(BlazorHostingMode hostingMode) : JwtBearerEvents
{
    private static readonly string[] AuthEndpointSegments =
    [
        $"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}/{RouteSegment.Refresh}",
        $"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}",
        $"/{KrafterRoute.ApiPrefix}/{KrafterRoute.ExternalAuth}/{RouteSegment.Google}",
        $"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}/{RouteSegment.Current}",
        $"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}/{RouteSegment.Logout}"
    ];

    /// <summary>
    /// Reads JWT from cookies (for SSR), handles SignalR query string tokens,
    /// and proactively refreshes expired tokens before validation.
    /// </summary>
    public override async Task MessageReceived(MessageReceivedContext context)
    {
        // SignalR query string token (needed for single-host; harmless for split-host)
        string? accessToken = context.Request.Query["access_token"];
        if (!string.IsNullOrEmpty(accessToken) &&
            context.HttpContext.Request.Path.StartsWithSegments($"/{KrafterRoute.ApiPrefix}/RealtimeHub"))
        {
            context.Token = accessToken;
            return;
        }

        string? cookieToken = context.Request.Cookies[StorageConstants.Local.AuthToken];

        // Skip refresh logic for auth endpoints — just pass the cookie token through
        if (IsAuthEndpoint(context.Request.Path))
        {
            context.Token = cookieToken;
            return;
        }

        // Proactive token refresh for expired tokens during SSR/prerendering
        string? refreshToken = context.Request.Cookies[StorageConstants.Local.RefreshToken];
        if (!string.IsNullOrEmpty(cookieToken) && IsTokenExpired(cookieToken) &&
            !string.IsNullOrWhiteSpace(refreshToken))
        {
            ILogger<BlazorJwtBearerEvents> logger =
                context.HttpContext.RequestServices.GetRequiredService<ILogger<BlazorJwtBearerEvents>>();
            IAuthApiService apiService =
                context.HttpContext.RequestServices.GetRequiredService<IAuthApiService>();

            logger.LogInformation("Token expired in OnMessageReceived, attempting refresh...");

            try
            {
                var refreshRequest = new RefreshTokenRequest
                {
                    Token = cookieToken, RefreshToken = refreshToken
                };
                Response<TokenResponse> refreshResponse =
                    await apiService.RefreshTokenAsync(refreshRequest, CancellationToken.None);

                if (refreshResponse is { Data: not null, IsError: false })
                {
                    cookieToken = refreshResponse.Data.Token;
                    if (refreshResponse.Data.Permissions is { Count: > 0 })
                    {
                        context.HttpContext.Items[StorageConstants.Local.Permissions] =
                            refreshResponse.Data.Permissions;
                    }

                    context.HttpContext.Items[StorageConstants.Local.AuthToken] = refreshResponse.Data.Token;
                    context.HttpContext.Items[StorageConstants.Local.RefreshToken] =
                        refreshResponse.Data.RefreshToken;
                    context.HttpContext.Items[StorageConstants.Local.AuthTokenExpiryDate] =
                        refreshResponse.Data.TokenExpiryTime;
                    context.HttpContext.Items[StorageConstants.Local.RefreshTokenExpiryDate] =
                        refreshResponse.Data.RefreshTokenExpiryTime;

                    // Persist refreshed tokens as cookies so the browser picks up the new values
                    IKrafterLocalStorageService localStorage = context.HttpContext.RequestServices
                        .GetRequiredService<IKrafterLocalStorageService>();
                    await localStorage.CacheAuthTokens(refreshResponse.Data);

                    logger.LogInformation("Token refreshed successfully in OnMessageReceived");
                }
                else
                {
                    logger.LogWarning("Token refresh failed or returned no data");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during proactive token refresh in OnMessageReceived");
            }
        }

        context.Token = cookieToken;
    }

    /// <summary>
    /// Enriches the validated identity with permission claims from the refresh response
    /// or from the server-side permission cache.
    /// </summary>
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity claimsIdentity)
        {
            return;
        }

        // Fresh permissions from a just-completed token refresh (stored in HttpContext.Items)
        if (context.HttpContext.Items.TryGetValue(StorageConstants.Local.Permissions,
                out object? freshPermissions) &&
            freshPermissions is List<string> permissionsFromRefresh)
        {
            AddPermissionClaims(claimsIdentity, permissionsFromRefresh);
            return;
        }

        // Fall back to cached permissions
        IKrafterLocalStorageService localStorage = context.HttpContext.RequestServices
            .GetRequiredService<IKrafterLocalStorageService>();
        ICollection<string>? cachedPermissions = await localStorage.GetCachedPermissionsAsync();
        if (cachedPermissions is { Count: > 0 })
        {
            AddPermissionClaims(claimsIdentity, cachedPermissions);
        }
    }

    /// <summary>
    /// SplitHost: always redirects to login.
    /// SingleHost: returns 401 JSON for API requests, redirects for browser requests.
    /// </summary>
    public override Task Challenge(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        if (hostingMode == BlazorHostingMode.SingleHost && IsApiRequest(context.Request))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsJsonAsync(new { message = "Authentication Failed." });
        }

        context.Response.Redirect("/login?ReturnUrl=" +
                                  Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
        return Task.CompletedTask;
    }

    /// <summary>
    /// SplitHost: default 403 behavior.
    /// SingleHost: returns 403 JSON for API requests, default behavior for browser requests.
    /// </summary>
    public override Task Forbidden(ForbiddenContext context)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        if (hostingMode == BlazorHostingMode.SingleHost && IsApiRequest(context.Request))
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsJsonAsync(
                new { message = "You are not authorized to access this resource." });
        }

        return Task.CompletedTask;
    }

    private static bool IsAuthEndpoint(PathString path) =>
        AuthEndpointSegments.Any(segment => path.StartsWithSegments(segment));

    private static bool IsApiRequest(HttpRequest request) =>
        request.Path.StartsWithSegments($"/{KrafterRoute.ApiPrefix}") ||
        request.Headers.Accept.Any(a => a != null && a.Contains("application/json"));

    private static bool IsTokenExpired(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.ReadToken(token) is JwtSecurityToken jwtToken)
            {
                return jwtToken.ValidTo <= DateTime.UtcNow.AddMinutes(1);
            }

            return true;
        }
        catch
        {
            return true;
        }
    }

    private static void AddPermissionClaims(ClaimsIdentity identity, IEnumerable<string> permissions)
    {
        foreach (string permission in permissions)
        {
            if (!string.IsNullOrWhiteSpace(permission) &&
                !identity.HasClaim(KrafterClaims.Permission, permission))
            {
                identity.AddClaim(new Claim(KrafterClaims.Permission, permission));
            }
        }
    }
}
