using AditiKraft.Krafter.Backend.Features.Auth.Common;
using Microsoft.AspNetCore.SignalR;

namespace AditiKraft.Krafter.Backend.Infrastructure.Realtime;

public static class RealtimeServiceRegistration
{
    public static IServiceCollection AddRealtimeServices(this IServiceCollection services)
    {
        services.AddSignalR()
            .AddHubOptions<RealtimeHub>(options => options.AddFilter<SessionHubFilter>());
        return services;
    }
}
