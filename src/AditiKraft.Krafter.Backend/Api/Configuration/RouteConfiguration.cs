using System.Reflection;
using AditiKraft.Krafter.Backend.Features.Auth;

namespace AditiKraft.Krafter.Backend.Api.Configuration;

public static class RouteConfiguration
{
    public static IServiceCollection AddRouteDiscovery(this IServiceCollection services)
    {
        Assembly assembly = typeof(Login.Route).Assembly;

        var routeRegistrars = assembly.GetTypes()
            .Where(t => typeof(IRouteRegistrar).IsAssignableFrom(t) &&
                        t is { IsInterface: false, IsAbstract: false } &&
                        t != typeof(IRouteRegistrar))
            .ToList();

        foreach (Type routeType in routeRegistrars)
        {
            services.AddSingleton(typeof(IRouteRegistrar), routeType);
        }

        return services;
    }

    public static IApplicationBuilder MapDiscoveredRoutes(this IApplicationBuilder app)
    {
        foreach (IRouteRegistrar registrar in app.ApplicationServices.GetServices<IRouteRegistrar>())
        {
            registrar.MapRoute((IEndpointRouteBuilder)app);
        }

        return app;
    }
}
