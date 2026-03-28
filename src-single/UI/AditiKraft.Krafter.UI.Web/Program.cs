using System.Text;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Web.Configuration;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.Aspire.ServiceDefaults;
using AditiKraft.Krafter.UI.Web.Client;
using AditiKraft.Krafter.UI.Web.Client.Common.Constants;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth.Common;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.AuthApi;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Storage;
using AditiKraft.Krafter.UI.Web.Components;
using AditiKraft.Krafter.UI.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Forwarded headers (proxy support)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Aspire observability + health checks
builder.AddServiceDefaults();

// ── Backend services (in-process) ──────────────────────────────────────────────
// Registers DB, Identity/auth, persistence, jobs, SignalR, swagger, validation, routes
builder.AddBackendServices();

// ── Blazor ─────────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHttpContextAccessor();

// ── Cache ──────────────────────────────────────────────────────────────────────
builder.AddRedisDistributedCache("cache");
builder.AddRedisOutputCache("cache");
builder.Services.AddHybridCache();

// ── Single-host JWT event overrides ────────────────────────────────────────────
// AddBackendServices() already configures JWT via ConfigureJwtBearerOptions.
// Override events to add cookie-reading for SSR and redirect-to-login for browser requests.
builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    Func<MessageReceivedContext, Task>? originalOnMessageReceived = options.Events?.OnMessageReceived;

    options.Events ??= new JwtBearerEvents();
    options.Events.OnMessageReceived = async context =>
    {
        // Skip refresh logic for auth API endpoints
        if (context.Request.Path.StartsWithSegments($"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}/{RouteSegment.Refresh}") ||
            context.Request.Path.StartsWithSegments($"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}") ||
            context.Request.Path.StartsWithSegments($"/{KrafterRoute.ApiPrefix}/{KrafterRoute.ExternalAuth}/{RouteSegment.Google}") ||
            context.Request.Path.StartsWithSegments($"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}/{RouteSegment.Current}") ||
            context.Request.Path.StartsWithSegments($"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}/{RouteSegment.Logout}"))
        {
            string? currentToken = context.Request.Cookies[StorageConstants.Local.AuthToken];
            context.Token = currentToken;
            return;
        }

        // Read token from cookie (for SSR/prerendered Blazor pages)
        string? cookieToken = context.Request.Cookies[StorageConstants.Local.AuthToken];
        string? refreshToken = context.Request.Cookies[StorageConstants.Local.RefreshToken];

        if (!string.IsNullOrEmpty(cookieToken) && IsTokenExpired(cookieToken) &&
            !string.IsNullOrWhiteSpace(refreshToken))
        {
            ILogger<Program> logger =
                context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
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

                    logger.LogInformation("Token refreshed successfully in OnMessageReceived");
                }
                else
                {
                    logger.LogWarning("No refresh token available for proactive refresh");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during proactive token refresh in OnMessageReceived");
            }

            context.Token = cookieToken;
        }
        else if (!string.IsNullOrEmpty(cookieToken))
        {
            context.Token = cookieToken;
        }
        else if (originalOnMessageReceived != null)
        {
            // Fall back to original behavior (query string token for SignalR)
            await originalOnMessageReceived(context);
        }
    };

    options.Events.OnTokenValidated = async context =>
    {
        if (context.Principal?.Identity is not ClaimsIdentity claimsIdentity)
        {
            return;
        }

        if (context.HttpContext.Items.TryGetValue(StorageConstants.Local.Permissions,
                out object? freshPermissions) &&
            freshPermissions is List<string> permissionsFromTempHttpContextItem)
        {
            foreach (string permission in permissionsFromTempHttpContextItem)
            {
                if (!string.IsNullOrWhiteSpace(permission) &&
                    !claimsIdentity.HasClaim(KrafterClaims.Permission, permission))
                {
                    claimsIdentity.AddClaim(new Claim(KrafterClaims.Permission, permission));
                }
            }
        }
        else
        {
            IKrafterLocalStorageService localStorage = context.HttpContext.RequestServices
                .GetRequiredService<IKrafterLocalStorageService>();
            ICollection<string>? permissionsFromMemoryCache = await localStorage.GetCachedPermissionsAsync();
            if (permissionsFromMemoryCache == null || permissionsFromMemoryCache.Count == 0)
            {
                return;
            }

            foreach (string permission in permissionsFromMemoryCache)
            {
                if (!string.IsNullOrWhiteSpace(permission) &&
                    !claimsIdentity.HasClaim(KrafterClaims.Permission, permission))
                {
                    claimsIdentity.AddClaim(new Claim(KrafterClaims.Permission, permission));
                }
            }
        }
    };

    options.Events.OnChallenge = context =>
    {
        context.HandleResponse();
        if (!context.Response.HasStarted)
        {
            // For API requests, return 401 JSON; for browser requests, redirect to login
            bool isApiRequest = context.Request.Path.StartsWithSegments($"/{KrafterRoute.ApiPrefix}") ||
                                context.Request.Headers.Accept.Any(a => a != null && a.Contains("application/json"));

            if (isApiRequest)
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsJsonAsync(new { message = "Authentication Failed." });
            }

            context.Response.Redirect("/login?ReturnUrl=" +
                                      Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
        }

        return Task.CompletedTask;
    };
});

// ── UI services ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ServerAuthenticationHandler>();
builder.Services.AddSingleton<IFormFactor, FormFactorServer>();
builder.Services.AddScoped<IAuthApiService, ServerAuthApiService>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IKrafterLocalStorageService, KrafterLocalStorageServiceServer>();
builder.Services.AddUIServices();
builder.Services.AddScoped<AuthenticationStateProvider, PersistingServerAuthenticationStateProvider>()
    .AddAuthorizationCore(RegisterPermissionClaimsClass.RegisterPermissionClaims);
builder.Services.AddRadzenComponents();
builder.Services.AddScoped<TenantIdentifier>();

// In single-host mode, RemoteHostUrl points to self since API routes are in the same process.
// WASM client still needs HTTP access to the API endpoints.
if (string.IsNullOrEmpty(builder.Configuration["RemoteHostUrl"]))
{
    // Fallback for standalone (non-Aspire) development
    builder.Configuration["RemoteHostUrl"] = "https://localhost:7291";
}

builder.Services.AddKrafterRefitClients();

// ── Build ──────────────────────────────────────────────────────────────────────
WebApplication app = builder.Build();

// ── Middleware pipeline (unified) ──────────────────────────────────────────────
app.UseForwardedHeaders();
app.UseResponseCompression();
app.MapDefaultEndpoints();
app.UseSwaggerConfiguration();
app.UseHttpsRedirection();
app.UseOutputCache();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", true);
    app.UseHsts();
}

// Backend middleware (exception handling, multi-tenancy, auth)
app.UseBackendMiddleware(builder.Configuration);

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.MapStaticAssets();
app.UseAntiforgery();

// ── Backend API routes (in-process) ────────────────────────────────────────────
app.MapBackendEndpoints();

// ── Blazor ─────────────────────────────────────────────────────────────────────
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(AditiKraft.Krafter.UI.Web.Client._Imports).Assembly);

app.MapGet("/cached", () => "Hello world!")
    .CacheOutput();

// BFF-only endpoints (cookie/token management for prerendered components)
// Login, refresh, and external auth are handled by backend routes (MapDiscoveredRoutes).
// Only cookie-specific endpoints (get current token, logout) need BFF mapping.
MapBffOnlyEndpoints(app);

app.Run();

static bool IsTokenExpired(string token)
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

static void MapBffOnlyEndpoints(WebApplication app)
{
    // Current token — reads from server-side cookie/cache, not from backend
    app.MapGet($"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}/{RouteSegment.Current}", async (IAuthApiService apiService) =>
    {
        Response<TokenResponse> res = await apiService.GetCurrentTokenAsync(CancellationToken.None);
        return Results.Json(res, statusCode: res.StatusCode);
    }).RequireAuthorization();

    // Logout — clears server-side cookie/cache
    app.MapPost($"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}/{RouteSegment.Logout}", async (IAuthApiService apiService) =>
    {
        await apiService.LogoutAsync(CancellationToken.None);
        return Results.Ok();
    });
}
