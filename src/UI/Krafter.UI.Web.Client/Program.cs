using Blazored.LocalStorage;
using Krafter.UI.Web.Client;
using Krafter.UI.Web.Client.Common.Validators;
using Krafter.UI.Web.Client.Features.Auth._Shared;
using Krafter.UI.Web.Client.Kiota;
using Krafter.UI.Web.Client.Infrastructure.Services;
using Krafter.UI.Web.Client.Infrastructure.Auth;
using Krafter.UI.Web.Client.Infrastructure.Storage;
using Krafter.UI.Web.Client.Infrastructure.Api;
using Krafter.UI.Web.Client.Infrastructure.Http;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Http;
using Radzen;

FluentValidationConfig.IsRunningOnUI = true;
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddRadzenComponents();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<IKrafterLocalStorageService, KrafterLocalStorageService>();
builder.Services.AddScoped<IApiService, ClientSideApiService>();
builder.Services.AddUIServices(builder.Configuration["RemoteHostUrl"]);

builder.Services.AddSingleton<IHttpContextAccessor, NullHttpContextAccessor>();
builder.Services.AddScoped<WebAssemblyAuthenticationHandler>();
builder.Services.AddScoped<TenantIdentifier>();
builder.Services.AddHttpClient("KrafterUIAPI", client =>
{
    HttpClientTenantConfigurator.SetAPITenantHttpClientDefaults(builder.Services, builder.Configuration["RemoteHostUrl"], client);
})
   .AddHttpMessageHandler<WebAssemblyAuthenticationHandler>()
    .Services
    .AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("KrafterUIAPI"));



builder.Services.AddHttpClient("KrafterUIBFF", client =>
{
    HttpClientTenantConfigurator.SetBFFTenantHttpClientDefaults(builder.Services, builder.Configuration["RemoteHostUrl"], client);
})
  .Services
  .AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("KrafterUIBFF"));
builder.Services.AddScoped<AuthenticationStateProvider, UIAuthenticationStateProvider>()
     .AddAuthorizationCore(RegisterPermissionClaimsClass.RegisterPermissionClaims);


builder.Services.AddKrafterKiotaClient(builder.Configuration["RemoteHostUrl"]);

await builder.Build().RunAsync();