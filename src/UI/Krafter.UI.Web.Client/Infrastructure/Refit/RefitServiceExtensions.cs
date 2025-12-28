using Krafter.UI.Web.Client.Infrastructure.Http;
using Refit;

namespace Krafter.UI.Web.Client.Infrastructure.Refit;

/// <summary>
/// Extension methods for registering Refit API clients.
/// </summary>
public static class RefitServiceExtensions
{
    /// <summary>
    /// Registers all Refit API clients with the DI container.
    /// Auth APIs use BFF (clientBaseAddress from TenantIdentifier), other APIs use Backend URL (dynamic per tenant).
    /// URLs are rewritten at runtime by RefitTenantHandler based on tenant subdomain.
    /// </summary>
    public static IServiceCollection AddKrafterRefitClients(this IServiceCollection services)
    {
        // Register auth handler
        services.AddTransient<RefitAuthHandler>();

        var refitSettings = new RefitSettings { ContentSerializer = new SystemTextJsonContentSerializer() };
        
        // Placeholder URL - will be rewritten by RefitTenantHandler at runtime
        const string placeholderUrl = "https://placeholder.local";

        // Register IAuthApi pointing to BFF (for cookie-based token management)
        // URL is rewritten by RefitTenantHandler to clientBaseAddress
        services.AddRefitClient<IAuthApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(placeholderUrl))
            .AddHttpMessageHandler(sp => new RefitTenantHandler(sp.GetRequiredService<TenantIdentifier>(), isBffClient: true));

        // Register authenticated API clients pointing to Backend directly
        // URL is rewritten by RefitTenantHandler to tenant-specific backendUrl
        services.AddRefitClient<IUsersApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(placeholderUrl))
            .AddHttpMessageHandler(sp => new RefitTenantHandler(sp.GetRequiredService<TenantIdentifier>(), isBffClient: false))
            .AddHttpMessageHandler<RefitAuthHandler>();

        services.AddRefitClient<IRolesApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(placeholderUrl))
            .AddHttpMessageHandler(sp => new RefitTenantHandler(sp.GetRequiredService<TenantIdentifier>(), isBffClient: false))
            .AddHttpMessageHandler<RefitAuthHandler>();

        services.AddRefitClient<ITenantsApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(placeholderUrl))
            .AddHttpMessageHandler(sp => new RefitTenantHandler(sp.GetRequiredService<TenantIdentifier>(), isBffClient: false))
            .AddHttpMessageHandler<RefitAuthHandler>();

        services.AddRefitClient<IAppInfoApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(placeholderUrl))
            .AddHttpMessageHandler(sp => new RefitTenantHandler(sp.GetRequiredService<TenantIdentifier>(), isBffClient: false));

        return services;
    }
}
