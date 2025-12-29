using System.Reflection;
using Backend.Features;

namespace Backend.Infrastructure.Persistence;

public static class PersistenceConfiguration
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        Assembly assembly = typeof(IScopedService).Assembly;

        // Register IScopedService implementations (legacy services with interfaces)
        var scopedServices = assembly.GetTypes()
            .Where(t => typeof(IScopedService).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
            .ToList();

        foreach (Type serviceType in scopedServices)
        {
            var interfaces = serviceType.GetInterfaces()
                .Where(i => i != typeof(IScopedService))
                .ToList();

            foreach (Type serviceInterface in interfaces)
            {
                services.AddScoped(serviceInterface, serviceType);
            }
        }

        // Register IScopedHandler implementations (VSA handlers - no interface)
        var scopedHandlers = assembly.GetTypes()
            .Where(t => typeof(IScopedHandler).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
            .ToList();

        foreach (Type handlerType in scopedHandlers)
        {
            services.AddScoped(handlerType);
        }

        return services;
    }
}
