using System.Text.Json;
using System.Text.Json.Serialization;
using Krafter.UI.Web.Client.Infrastructure.Http;
using Refit;

namespace Krafter.UI.Web.Client.Infrastructure.Refit;

public static class RefitServiceExtensions
{
    public static IServiceCollection AddKrafterRefitClients(this IServiceCollection services)
    {
        // Register auth handler
        services.AddTransient<RefitAuthHandler>();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonNumberEnumConverter<EntityKind>());
        var refitSettings = new RefitSettings { ContentSerializer = new SystemTextJsonContentSerializer(options) };

        // Placeholder URL - will be rewritten by RefitTenantHandler at runtime
        const string placeholderUrl = "https://placeholder.local";

        // Register IAuthApi pointing to BFF (for cookie-based token management)
        // URL is rewritten by RefitTenantHandler to clientBaseAddress
        services.AddRefitClient<IAuthApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(placeholderUrl))
            .AddHttpMessageHandler(sp => new RefitTenantHandler(sp.GetRequiredService<TenantIdentifier>(), true));

        // Register authenticated API clients pointing to Backend directly
        // URL is rewritten by RefitTenantHandler to tenant-specific backendUrl
        services.AddRefitClient<IUsersApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(placeholderUrl))
            .AddHttpMessageHandler(sp => new RefitTenantHandler(sp.GetRequiredService<TenantIdentifier>(), false))
            .AddHttpMessageHandler<RefitAuthHandler>();

        services.AddRefitClient<IRolesApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(placeholderUrl))
            .AddHttpMessageHandler(sp => new RefitTenantHandler(sp.GetRequiredService<TenantIdentifier>(), false))
            .AddHttpMessageHandler<RefitAuthHandler>();

        services.AddRefitClient<ITenantsApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(placeholderUrl))
            .AddHttpMessageHandler(sp => new RefitTenantHandler(sp.GetRequiredService<TenantIdentifier>(), false))
            .AddHttpMessageHandler<RefitAuthHandler>();

        services.AddRefitClient<IAppInfoApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(placeholderUrl))
            .AddHttpMessageHandler(sp => new RefitTenantHandler(sp.GetRequiredService<TenantIdentifier>(), false));

        return services;
    }
}
