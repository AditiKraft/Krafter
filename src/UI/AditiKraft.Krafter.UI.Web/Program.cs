using AditiKraft.Krafter.Aspire.ServiceDefaults;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.UI.Web.Components;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;


WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOptions<JwtSettings>()
    .BindConfiguration($"SecuritySettings:{nameof(JwtSettings)}")
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureBlazorJwtBearerOptions>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });

builder.Services.AddUiHostServices(builder.Configuration);
WebApplication app = builder.Build();

app.MapDefaultEndpoints();
app.MapUiUrlConfiguration();
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

// Intercept auth responses (login, refresh, external-auth) to set HttpOnly cookies
app.UseMiddleware<AuthCookieMiddleware>();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(AditiKraft.Krafter.UI.Web.Client._Imports).Assembly);

MapAuthTokenEndpoints(app);

app.Run();

static void MapAuthTokenEndpoints(WebApplication app)
{
    app.MapGet($"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}/{RouteSegment.Current}", async (IAuthApiService apiService) =>
    {
        Response<TokenResponse> res = await apiService.GetCurrentTokenAsync(CancellationToken.None);
        return Results.Json(res, statusCode: res.StatusCode);
    }).RequireAuthorization();

    app.MapPost($"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}", async ([FromBody] TokenRequest request, IAuthApiService apiService,
        [FromServices] IHttpClientFactory clientFactory) =>
    {
        Response<TokenResponse> res = await apiService.CreateTokenAsync(request, CancellationToken.None);
        return Results.Json(res, statusCode: res.StatusCode);
    });
    app.MapPost($"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}/{RouteSegment.Refresh}", async ([FromBody] RefreshTokenRequest request,
        IAuthApiService apiService,
        [FromServices] IHttpClientFactory clientFactory) =>
    {
        Response<TokenResponse> tokenResponse = await apiService.RefreshTokenAsync(request, CancellationToken.None);
        return Results.Json(tokenResponse, statusCode: tokenResponse.StatusCode);
    });

    app.MapPost($"/{ApiRoutes.ApiPrefix}/{ApiRoutes.ExternalAuth}/{RouteSegment.Google}",
        async ([FromBody] TokenRequest request, IAuthApiService apiService) =>
        {
            Response<TokenResponse> tokenResponse = await apiService.ExternalAuthAsync(request, CancellationToken.None);
            return Results.Json(tokenResponse, statusCode: tokenResponse.StatusCode);
        });

    app.MapPost($"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}/{RouteSegment.Logout}", async (IAuthApiService apiService) =>
    {
        await apiService.LogoutAsync(CancellationToken.None);
        return Results.Ok();
    });
}
