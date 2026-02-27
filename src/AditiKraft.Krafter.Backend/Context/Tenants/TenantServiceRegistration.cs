using AditiKraft.Krafter.Backend.Common.Interfaces;

namespace AditiKraft.Krafter.Backend.Context.Tenants;

public static class TenantServiceRegistration
{
    public static IServiceCollection AddTenantServices(this IServiceCollection services)
    {
        services.AddScopedAs<CurrentTenantService>(new[]
        {
            typeof(ITenantGetterService), typeof(ITenantSetterService)
        });

        return services;
    }

    private static IServiceCollection AddScopedAs<T>(this IServiceCollection services, IEnumerable<Type> types)
        where T : class
    {
        // register the type first
        services.AddScoped<T>();
        foreach (Type type in types)
        {
            // register a scoped 
            services.AddScoped(type, svc =>
            {
                T rs = svc.GetRequiredService<T>();
                return rs;
            });
        }

        return services;
    }
}

