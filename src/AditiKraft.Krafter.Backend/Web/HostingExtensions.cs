using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using System.Threading.RateLimiting;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence.Tenants;
using AditiKraft.Krafter.Backend.Infrastructure.Realtime;
using AditiKraft.Krafter.Backend.Web.Authentication;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Backend.Web.Configuration;
using AditiKraft.Krafter.Backend.Web.Middleware;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using FluentValidation;
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
        AppUrls urls = builder.Configuration.GetSection(AppUrls.SectionName).Get<AppUrls>() ?? new AppUrls();
        _ = urls.GetRootUiUri();
        _ = urls.GetTenantBaseUri();
        _ = urls.GetApiUri();
        _ = urls.GetServerApiUri();
        builder.Services.AddSingleton(urls);

        builder.Services.AddDatabaseConfiguration(builder.Configuration);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<TwoFactorService>();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        builder.Services.AddScoped<SessionValidator>();
        builder.Services.AddHostedService<TwoFactorCleanup>();
        IDataProtectionBuilder protection = builder.Services.AddDataProtection().SetApplicationName("AditiKraft.Krafter");
        string? keyRing = builder.Configuration["TwoFactor:KeyRingPath"];
        if (!string.IsNullOrWhiteSpace(keyRing)) protection.PersistKeysToFileSystem(new DirectoryInfo(keyRing));
        string? certificatePath = builder.Configuration["TwoFactor:KeyEncryptionCertificatePath"];
        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath,
                builder.Configuration["TwoFactor:KeyEncryptionCertificatePassword"]);
            using RSA? encryptionKey = certificate.GetRSAPublicKey();
            if (!certificate.HasPrivateKey || encryptionKey is null)
                throw new InvalidOperationException("The two-factor key-encryption certificate must include an RSA private key.");
            protection.ProtectKeysWithCertificate(certificate);
        }
        builder.Services.Configure<TwoFactorProtectionOptions>(options => options.EnrollmentAllowed =
            builder.Environment.IsDevelopment() || (!string.IsNullOrWhiteSpace(keyRing) && !string.IsNullOrWhiteSpace(certificatePath)));


        builder.Services.AddCurrentUserServices();
        builder.Services.AddTenantServices();
        builder.Services.AddScoped<MultiTenantServiceMiddleware>();
        builder.Services.AddScoped<ITenantFinderService, TenantFinderService>();

        builder.Services.AddScoped<ExceptionMiddleware>();
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddAuthorization();
        builder.Services.AddAuthServices(builder.Configuration);

        builder.Services.AddApplicationServices();

        builder.Services.AddNotificationServices(builder.Configuration);

        builder.AddFluentValidationEndpointFilter();
        builder.Services.AddValidatorsFromAssemblyContaining<TokenRequestValidator>();

        builder.Services.AddBackgroundJobs();

        builder.Services.AddRouteDiscovery();

        builder.Services.AddSignalR(options => options.AddFilter<SessionHubFilter>());

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
        app.UseRateLimiter();

        return app;
    }

    /// <summary>
    /// Maps backend endpoints: VSA routes, SignalR hub, background job dashboard.
    /// Call after middleware pipeline is configured.
    /// </summary>
    public static WebApplication MapBackendEndpoints(this WebApplication app)
    {
        app.MapDiscoveredRoutes();

        app.MapHub<RealtimeHub>($"/{ApiRoutes.ApiPrefix}/{nameof(RealtimeHub)}", options => options.CloseOnAuthenticationExpiration = true)
            .MustHavePermission(PermissionAction.View, PermissionResource.Notifications);

        app.UseBackgroundJobs();

        return app;
    }
}
