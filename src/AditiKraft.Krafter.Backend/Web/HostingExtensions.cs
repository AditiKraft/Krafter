using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Backend.Web.Middleware;
using AditiKraft.Krafter.Backend.Infrastructure.Realtime;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence.Tenants;
using AditiKraft.Krafter.Backend.Web.Configuration;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Common.Context.Tenants;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using FluentValidation;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.Backend.Common.Interfaces;
using Microsoft.AspNetCore.ResponseCompression;

namespace AditiKraft.Krafter.Backend.Web;

/// <summary>
/// Reusable backend service registration and middleware configuration.
/// Used by both the standalone Backend host (split-host) and the combined UI.Web host (single-host).
/// </summary>
public static class HostingExtensions
{
    /// <summary>
    /// Registers all backend services: database, auth, persistence, jobs, SignalR, swagger, validation, route discovery.
    /// </summary>
    public static WebApplicationBuilder AddBackendServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddDatabaseConfiguration(builder.Configuration);

        builder.Services.AddCurrentUserServices();
        builder.Services.AddTenantServices();
        builder.Services.AddScoped<MultiTenantServiceMiddleware>();
        builder.Services.AddScoped<ITenantFinderService, TenantFinderService>();

        builder.Services.AddScoped<ExceptionMiddleware>();
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddAuthorization();
        builder.Services.AddAuthServices(builder.Configuration);

        builder.Services.AddPersistenceServices();

        builder.Services.AddNotificationServices(builder.Configuration);

        builder.AddFluentValidationEndpointFilter();
        builder.Services.AddValidatorsFromAssemblyContaining<TokenRequestValidator>();

        builder.Services.AddBackgroundJobs();

        builder.Services.AddRouteDiscovery();

        builder.Services.AddSignalR();

        builder.Services.AddResponseCompression(opts =>
        {
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/octet-stream"]);
        });

        builder.Services.AddSwaggerConfiguration();

        return builder;
    }

    /// <summary>
    /// Applies backend middleware pipeline: exception handling, multi-tenancy, auth.
    /// Call after UseForwardedHeaders/UseResponseCompression and before endpoint mapping.
    /// </summary>
    public static WebApplication UseBackendMiddleware(this WebApplication app, IConfiguration config)
    {
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseMiddleware<MultiTenantServiceMiddleware>();
        app.AuthMiddleware(config);

        return app;
    }

    /// <summary>
    /// Maps backend endpoints: VSA routes, SignalR hub, background job dashboard.
    /// Call after middleware pipeline is configured.
    /// </summary>
    public static WebApplication MapBackendEndpoints(this WebApplication app)
    {
        app.MapDiscoveredRoutes();

        app.MapHub<RealtimeHub>($"/{nameof(RealtimeHub)}")
            .MustHavePermission(KrafterAction.View, KrafterResource.Notifications);

        app.UseBackgroundJobs();

        return app;
    }
}
