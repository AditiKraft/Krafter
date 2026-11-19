using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using TickerQ.Dashboard.DependencyInjection;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.DependencyInjection;

namespace AditiKraft.Krafter.Backend.Infrastructure.Jobs;

public static class JobsConfiguration
{
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment() &&
            (string.IsNullOrWhiteSpace(configuration["TickerQBasicAuth:Username"]) ||
             string.IsNullOrWhiteSpace(configuration["TickerQBasicAuth:Password"]) ||
             string.Equals(configuration["TickerQBasicAuth:Password"]?.Trim(), "admin", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Configure private TickerQBasicAuth:Username and TickerQBasicAuth:Password values outside Development; the public admin password is not allowed.");
        }

        services.AddScoped<IJobService, JobService>();

        services.AddTickerQ(options =>
        {
            options.SetMaxConcurrency(4);

            options.AddOperationalStore<BackgroundJobsContext>(efOpt =>
            {
                // efOpt.CancelMissedTickersOnApplicationRestart();
            });

            options.AddDashboard(uiopt =>
            {
                uiopt.BasePath = "/tickerq-dashboard";
                uiopt.EnableBasicAuth = true;
            });
        });

        return services;
    }

    public static IApplicationBuilder UseBackgroundJobs(this IApplicationBuilder app)
    {
        app.UseTickerQ();
        return app;
    }
}



