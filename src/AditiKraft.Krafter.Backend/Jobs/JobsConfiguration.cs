using AditiKraft.Krafter.Backend.Infrastructure.BackgroundJobs;
using TickerQ.Dashboard.DependencyInjection;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.DependencyInjection;

namespace AditiKraft.Krafter.Backend.Jobs;

public static class JobsConfiguration
{
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services)
    {
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

