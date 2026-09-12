using System.Net.Http.Json;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.UI.Web.Client;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Http;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
// Load public URLs from this UI host so deployments configure them only on the server.
using (var configurationClient = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) })
{
    AppUrls urls = await configurationClient.GetFromJsonAsync<AppUrls>(AppUrls.ConfigurationPath)
        ?? throw new InvalidOperationException("The UI host did not return its public URL configuration.");
    _ = urls.GetRootUiUri();
    _ = urls.GetApiUri();
    builder.Services.AddSingleton(urls);
}
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddRadzenComponents();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<IAuthStorageService, AuthStorageService>();
builder.Services.AddScoped<IAuthApiService, ClientAuthApiService>();
// Browser API handlers use separate DI scopes but share the same token storage.
builder.Services.AddSingleton<TokenRefreshCoordinator>();

builder.Services.AddUIServices();
builder.Services.AddSingleton<IHttpContextAccessor, NullHttpContextAccessor>();
builder.Services.AddScoped<TenantIdentifier>();

builder.Services.AddScoped<AuthenticationStateProvider, UIAuthenticationStateProvider>()
    .AddAuthorizationCore(PermissionRegistration.RegisterPermissionClaims);

builder.Services.AddCascadingAuthenticationState();

// URLs are rewritten dynamically by RefitTenantHandler based on tenant
builder.Services.AddApiRefitClients();

await builder.Build().RunAsync();
