using Krafter.UI.Web.Client.Infrastructure.Services;
using Refit;

namespace Krafter.UI.Web.Client.Infrastructure.Refit;

/// <summary>
/// Extension methods for registering Refit API clients.
/// </summary>
public static class RefitServiceExtensions
{
    /// <summary>
    /// Registers all Refit API clients with the DI container.
    /// </summary>
    public static IServiceCollection AddKrafterRefitClients(this IServiceCollection services, string baseUrl)
    {
        // Register handlers
        services.AddTransient<RefitAuthHandler>();
        services.AddTransient<RefitTenantHandler>();

        var refitSettings = new RefitSettings { ContentSerializer = new SystemTextJsonContentSerializer() };

        // Register IAuthApi (no auth handler - used for login/refresh)
        services.AddRefitClient<IAuthApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler<RefitTenantHandler>();

        // Register authenticated API clients
        services.AddRefitClient<IUsersApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler<RefitTenantHandler>()
            .AddHttpMessageHandler<RefitAuthHandler>();

        services.AddRefitClient<IRolesApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler<RefitTenantHandler>()
            .AddHttpMessageHandler<RefitAuthHandler>();

        services.AddRefitClient<ITenantsApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler<RefitTenantHandler>()
            .AddHttpMessageHandler<RefitAuthHandler>();

        services.AddRefitClient<IAppInfoApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler<RefitTenantHandler>();

        return services;
    }
}
