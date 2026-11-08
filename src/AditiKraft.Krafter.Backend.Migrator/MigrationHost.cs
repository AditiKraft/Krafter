using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AditiKraft.Krafter.Backend.Migrator;

public static class MigrationHost
{
    // A canceled migration is incomplete, so it must not release an orchestrator's
    // success gate. Successful completion remains zero even if shutdown follows.
    public static async Task<int> RunAsync(IHost host, Func<CancellationToken, Task> initializeAsync)
    {
        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MigrationHost));
        IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        try
        {
            await host.StartAsync();
            await initializeAsync(lifetime.ApplicationStopping);
            return 0;
        }
        catch (OperationCanceledException) when (lifetime.ApplicationStopping.IsCancellationRequested)
        {
            logger.LogInformation("Database migration canceled by shutdown before completion.");
            return 2;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Database migration failed.");
            return 1;
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }
}
