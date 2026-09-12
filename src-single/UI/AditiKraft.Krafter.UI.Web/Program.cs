using AditiKraft.Krafter.Aspire.ServiceDefaults;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Web.Configuration;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.UI.Web.Components;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;

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

// ── Single-host JWT event overrides ────────────────────────────────────────────
// AddBackendServices() configures JWT for API-only mode (throws exceptions on challenge/forbidden).
// Override events with BlazorJwtBearerEvents for cookie-reading, token refresh, and login redirects.
builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.Events = new BlazorJwtBearerEvents(BlazorHostingMode.SingleHost);
});

builder.Services.AddUiHostServices(builder.Configuration, BlazorHostingMode.SingleHost);

// ── Build ──────────────────────────────────────────────────────────────────────
WebApplication app = builder.Build();

// ── Middleware pipeline (unified) ──────────────────────────────────────────────
app.UseForwardedHeaders();
app.UseResponseCompression();
app.MapDefaultEndpoints();
app.MapUiUrlConfiguration();
app.UseSwaggerConfiguration();
app.UseHttpsRedirection();

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

// Intercept auth responses (login, refresh, external-auth) to set HttpOnly cookies
app.UseMiddleware<AuthCookieMiddleware>();

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

// BFF-only endpoints(cookie/token management for prerendered components)
// Login, refresh, and external auth are handled by backend routes (MapDiscoveredRoutes).
// Only cookie-specific endpoints (get current token, logout) need BFF mapping.
MapBffOnlyEndpoints(app);

app.Run();

static void MapBffOnlyEndpoints(WebApplication app)
{
    // Current token — reads from server-side cookie/cache, not from backend
    app.MapGet($"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}/{RouteSegment.Current}", async (IAuthApiService apiService) =>
    {
        Response<TokenResponse> res = await apiService.GetCurrentTokenAsync(CancellationToken.None);
        return Results.Json(res, statusCode: res.StatusCode);
    }).RequireAuthorization();

    // Logout — clears server-side cookie/cache
    app.MapPost($"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}/{RouteSegment.Logout}", async (IAuthApiService apiService) =>
    {
        await apiService.LogoutAsync(CancellationToken.None);
        return Results.Ok();
    });
}
