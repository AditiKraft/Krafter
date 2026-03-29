using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Web.Configuration;
using AditiKraft.Krafter.Aspire.ServiceDefaults;
using Microsoft.AspNetCore.HttpOverrides;

namespace AditiKraft.Krafter.Backend;

public static class Program
{
    private static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Forwarded headers (proxy support)
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        // Aspire observability + health checks
        builder.AddServiceDefaults();

        // CORS (split-host needs cross-origin support)
        builder.Services.AddCorsConfiguration(builder.Configuration, builder.Environment);

        // All backend services (DB, auth, persistence, jobs, SignalR, swagger, validation, routes)
        builder.AddBackendServices();

        // Build app
        WebApplication app = builder.Build();

        // Middleware Pipeline
        app.UseForwardedHeaders();
        app.UseResponseCompression();
        app.MapDefaultEndpoints();

        app.UseCorsConfiguration();
        app.UseSwaggerConfiguration();
        app.UseHttpsRedirection();

        app.UseBackendMiddleware(builder.Configuration);

        // Backend endpoints (VSA routes, SignalR hub, background jobs dashboard)
        app.MapBackendEndpoints();

        app.Run();
    }
}



