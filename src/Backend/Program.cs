using Backend.Api;
using Backend.Api.Authorization;
using Backend.Api.Configuration;
using Backend.Api.Middleware;
using Backend.Application.BackgroundJobs;
using Backend.Application.Multitenant;
using Backend.Application.Notifications;
using Backend.Common.Auth.Permissions;
using Backend.Common.Interfaces;
using Backend.Features.Auth.Token;
using Backend.Hubs;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Tenants;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;

namespace Backend;

public static class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

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
        builder.Services.AddValidatorsFromAssemblyContaining<GetToken.TokenRequestValidator>();

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
        var app = builder.Build();

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