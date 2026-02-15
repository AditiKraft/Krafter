using AditiKraft.Krafter.UI.Web.Client;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth._Shared;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Api;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Storage;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Http;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddRadzenComponents();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<IKrafterLocalStorageService, KrafterLocalStorageService>();
builder.Services.AddScoped<IApiService, ClientSideApiService>();

builder.Services.AddUIServices();
builder.Services.AddSingleton<IHttpContextAccessor, NullHttpContextAccessor>();
builder.Services.AddScoped<TenantIdentifier>();

builder.Services.AddScoped<AuthenticationStateProvider, UIAuthenticationStateProvider>()
    .AddAuthorizationCore(RegisterPermissionClaimsClass.RegisterPermissionClaims);

builder.Services.AddCascadingAuthenticationState();

// URLs are rewritten dynamically by RefitTenantHandler based on tenant
builder.Services.AddKrafterRefitClients();

await builder.Build().RunAsync();
