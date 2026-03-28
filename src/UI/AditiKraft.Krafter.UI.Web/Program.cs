using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Aspire.ServiceDefaults;
using AditiKraft.Krafter.UI.Web.Client;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth.Common;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.AuthApi;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using AditiKraft.Krafter.UI.Web.Components;
using AditiKraft.Krafter.UI.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;


WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHttpContextAccessor();
builder.AddRedisDistributedCache("cache");
builder.AddRedisOutputCache("cache");
builder.Services.AddHybridCache();
builder.Services.AddScoped<ServerAuthenticationHandler>();
builder.Services.AddSingleton<IFormFactor, FormFactorServer>();
builder.Services.AddScoped<IAuthApiService, ServerAuthApiService>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddOptions<JwtSettings>()
    .BindConfiguration($"SecuritySettings:{nameof(JwtSettings)}")
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureBlazorJwtBearerOptions>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, null!);

string? apiUrl = builder.Configuration.GetValue<string>("services:krafter-api:https:0");
if (string.IsNullOrWhiteSpace(apiUrl))
{
    throw new Exception("API URL not found");
}

// Override RemoteHostUrl with Aspire service discovery URL for server-side Refit calls
builder.Configuration["RemoteHostUrl"] = apiUrl;

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IKrafterLocalStorageService, KrafterLocalStorageServiceServer>();
builder.Services.AddUIServices();
builder.Services.AddScoped<AuthenticationStateProvider, PersistingServerAuthenticationStateProvider>()
    .AddAuthorizationCore(RegisterPermissionClaimsClass.RegisterPermissionClaims);
builder.Services.AddRadzenComponents();
builder.Services.AddScoped<TenantIdentifier>();

// Server uses apiUrl for both AditiKraft.Krafter.Backend and BFF since it manages cookies directly
builder.Services.AddKrafterRefitClients();
WebApplication app = builder.Build();
app.UseOutputCache();

app.MapDefaultEndpoints();
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.MapStaticAssets();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(AditiKraft.Krafter.UI.Web.Client._Imports).Assembly);
app.MapGet("/cached", () => "Hello world!")
    .CacheOutput();
MapAuthTokenEndpoints(app);

app.Run();

static void MapAuthTokenEndpoints(WebApplication app)
{
    app.MapGet($"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}/{RouteSegment.Current}", async (IAuthApiService apiService) =>
    {
        Response<TokenResponse> res = await apiService.GetCurrentTokenAsync(CancellationToken.None);
        return Results.Json(res, statusCode: res.StatusCode);
    }).RequireAuthorization();

    app.MapPost($"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}", async ([FromBody] TokenRequest request, IAuthApiService apiService,
        [FromServices] IHttpClientFactory clientFactory) =>
    {
        Response<TokenResponse> res = await apiService.CreateTokenAsync(request, CancellationToken.None);
        return Results.Json(res, statusCode: res.StatusCode);
    });
    app.MapPost($"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}/{RouteSegment.Refresh}", async ([FromBody] RefreshTokenRequest request,
        IAuthApiService apiService,
        [FromServices] IHttpClientFactory clientFactory) =>
    {
        Response<TokenResponse> tokenResponse = await apiService.RefreshTokenAsync(request, CancellationToken.None);
        return Results.Json(tokenResponse, statusCode: tokenResponse.StatusCode);
    });

    app.MapPost($"/{KrafterRoute.ApiPrefix}/{KrafterRoute.ExternalAuth}/{RouteSegment.Google}",
        async ([FromBody] TokenRequest request, IAuthApiService apiService) =>
        {
            Response<TokenResponse> tokenResponse = await apiService.ExternalAuthAsync(request, CancellationToken.None);
            return Results.Json(tokenResponse, statusCode: tokenResponse.StatusCode);
        });

    app.MapPost($"/{KrafterRoute.ApiPrefix}/{KrafterRoute.Tokens}/{RouteSegment.Logout}", async (IAuthApiService apiService) =>
    {
        await apiService.LogoutAsync(CancellationToken.None);
        return Results.Ok();
    });
}



