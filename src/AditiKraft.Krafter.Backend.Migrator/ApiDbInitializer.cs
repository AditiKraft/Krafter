using System.Diagnostics;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AditiKraft.Krafter.Backend.Migrator;

public sealed class ApiDbInitializer(
    IServiceProvider serviceProvider,
    IHostEnvironment hostEnvironment,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<ApiDbInitializer> logger) : BackgroundService
{
    private readonly ActivitySource _activitySource = new(hostEnvironment.ApplicationName);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using Activity? activity = _activitySource.StartActivity(hostEnvironment.ApplicationName, ActivityKind.Client);

        try
        {
            using IServiceScope scope = serviceProvider.CreateScope();

            await MigrateDbContextAsync<TenantDbContext>(scope.ServiceProvider, cancellationToken);
            await MigrateDbContextAsync<BackgroundJobsContext>(scope.ServiceProvider, cancellationToken);
            await MigrateDbContextAsync<KrafterContext>(scope.ServiceProvider, cancellationToken);

            logger.LogInformation("Database migration completed.");
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            logger.LogError(ex, "Database migration failed.");
            throw;
        }
        finally
        {
            hostApplicationLifetime.StopApplication();
        }
    }

    private async Task MigrateDbContextAsync<TContext>(IServiceProvider scopedServices, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        TContext dbContext = scopedServices.GetRequiredService<TContext>();

        logger.LogInformation("Applying migrations for {DbContext}.", typeof(TContext).Name);

        await EnsureDatabaseAsync(dbContext, cancellationToken);
        await RunMigrationAsync(dbContext, cancellationToken);
    }

    private static async Task EnsureDatabaseAsync(DbContext dbContext, CancellationToken cancellationToken)
    {
        IRelationalDatabaseCreator dbCreator = dbContext.GetService<IRelationalDatabaseCreator>();
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            if (!await dbCreator.ExistsAsync(cancellationToken))
            {
                await dbCreator.CreateAsync(cancellationToken);
            }
        });
    }

    private static async Task RunMigrationAsync(DbContext dbContext, CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        });
    }
}
