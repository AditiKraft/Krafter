using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Net;

namespace AditiKraft.Krafter.Aspire.ServiceDefaults;

// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Retain the framework's loopback defaults. Trust additional immediate proxy hops explicitly.
            foreach (string address in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
                options.KnownProxies.Add(IPAddress.Parse(address));
            foreach (string network in builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [])
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        });

        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler(options =>
            {
                // One-use credentials and mutations cannot be replayed after an uncertain response.
                // Safe reads retain the standard retry policy.
                options.Retry.DisableForUnsafeHttpMethods();

                // Per-client throttling does not mean the shared dependency is unavailable.
                // Preserve the default handling of 408, 5xx, transport errors and timeouts.
                var handlesDependencyFailure = options.CircuitBreaker.ShouldHandle;
                options.CircuitBreaker.ShouldHandle = arguments =>
                    arguments.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests
                        ? ValueTask.FromResult(false)
                        : handlesDependencyFailure(arguments);
            });

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation()
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        bool useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
            if (!builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(builder.Configuration["SeqUrl"]))
            {
                // SeqUrl and SeqApiKey should be provided via environment variables or user secrets.
                // Do NOT commit SeqApiKey or other sensitive keys to source control.
                builder.AddExportToSeq();
            }
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks("/health");

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks("/alive", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
        }

        return app;
    }

    private static IHostApplicationBuilder AddExportToSeq(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<OpenTelemetryLoggerOptions>(logging => logging.AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri($"{builder.Configuration["SeqUrl"]}/ingest/otlp/v1/logs");
            opt.Protocol = OtlpExportProtocol.HttpProtobuf;
            opt.Headers = $@"X-Seq-ApiKey={builder.Configuration["SeqApiKey"]}";
        }));
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing
            .AddSource("MyApp.Source")
            .AddOtlpExporter(opt =>
                {
                    opt.Endpoint = new Uri($"{builder.Configuration["SeqUrl"]}/ingest/otlp/v1/traces");
                    opt.Protocol = OtlpExportProtocol.HttpProtobuf;
                    //set header here
                    opt.Headers = $@"X-Seq-ApiKey={builder.Configuration["SeqApiKey"]}";
                }
            ));
        return builder;
    }
}
