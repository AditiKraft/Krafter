using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.UI.Web.Client;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace AditiKraft.Krafter.UI.Web.Infrastructure.Hosting;

public static class UiHostServiceRegistration
{
    public static IServiceCollection AddUiHostServices(this IServiceCollection services, IConfiguration configuration,
        BlazorHostingMode hostingMode = BlazorHostingMode.SplitHost)
    {
        services.AddSingleton(UiUrlConfiguration.Resolve(configuration, hostingMode));
        services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();
        services.AddHttpContextAccessor();

        services.AddDistributedPostgresCache(options =>
        {
            options.ConnectionString = configuration.GetConnectionString("appDb")
                ?? throw new InvalidOperationException("Connection string 'appDb' not found");
            options.SchemaName = "public";
            options.TableName = "cache";
            options.CreateIfNotExists = true;
        });
        services.AddHybridCache();
        services.AddMemoryCache();

        services.AddSingleton<IFormFactor, FormFactorServer>();
        services.AddScoped<IAuthApiService, ServerAuthApiService>();
        services.AddSingleton<IAuthStorageService, AuthStorageServiceServer>();
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, PersistingServerAuthenticationStateProvider>()
            .AddAuthorizationCore(PermissionRegistration.RegisterPermissionClaims);

        services.AddUIServices();
        services.AddRadzenComponents();
        services.AddScoped<TenantIdentifier>();
        services.AddApiRefitClients();
        return services;
    }
}
