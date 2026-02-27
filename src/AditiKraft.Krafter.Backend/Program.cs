using AditiKraft.Krafter.Backend.Web.Middleware;
using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Realtime;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence.Tenants;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Backend.Web.Configuration;
using AditiKraft.Krafter.Backend.Jobs;
using AditiKraft.Krafter.Backend.Context.Tenants;
using AditiKraft.Krafter.Backend.Notifications;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using FluentValidation;
using AditiKraft.Krafter.Aspire.ServiceDefaults;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using AditiKraft.Krafter.Contracts.Contracts.Auth;

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

        // CORS
        builder.Services.AddCorsConfiguration(builder.Configuration, builder.Environment);

        // Databases (TenantDb, KrafterContext, BackgroundJobsContext)
        builder.Services.AddDatabaseConfiguration(builder.Configuration);

        // Multi-tenancy
        builder.Services.AddCurrentUserServices();
        builder.Services.AddTenantServices();
        builder.Services.AddScoped<MultiTenantServiceMiddleware>();
        builder.Services.AddScoped<ITenantFinderService, TenantFinderService>();

        // Exception handling
        builder.Services.AddScoped<ExceptionMiddleware>();
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        // Authentication & Authorization
        builder.Services.AddAuthorization();
        builder.Services.AddAuthServices(builder.Configuration);

        // Auto-register IScopedService & IScopedHandler implementations
        builder.Services.AddPersistenceServices();

        // Notifications (SMTP)
        builder.Services.AddNotificationServices(builder.Configuration);

        // FluentValidation
        builder.AddFluentValidationEndpointFilter();
        builder.Services.AddValidatorsFromAssemblyContaining<TokenRequestValidator>();

        // Background Jobs (TickerQ)
        builder.Services.AddBackgroundJobs();

        // Route Discovery (VSA)
        builder.Services.AddRouteDiscovery();

        // SignalR
        builder.Services.AddSignalR();

        // Response Compression
        builder.Services.AddResponseCompression(opts =>
        {
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/octet-stream"]);
        });

        // Swagger/OpenAPI
        builder.Services.AddSwaggerConfiguration();

        // Build app
        WebApplication app = builder.Build();

        // Middleware Pipeline
        app.UseForwardedHeaders();
        app.UseResponseCompression();
        app.MapDefaultEndpoints(); // Aspire health checks

        app.UseCorsConfiguration();
        app.UseSwaggerConfiguration();
        app.UseHttpsRedirection();

        app.UseMiddleware<ExceptionMiddleware>();
        app.UseMiddleware<MultiTenantServiceMiddleware>();
        app.AuthMiddleware(builder.Configuration);

        app.UseBackgroundJobs(); // TickerQ dashboard

        // VSA Route Discovery
        app.MapDiscoveredRoutes();

        // SignalR Hub
        app.MapHub<RealtimeHub>($"/{nameof(RealtimeHub)}")
            .MustHavePermission(KrafterAction.View, KrafterResource.Notifications);

        app.Run();
    }
}


