using Backend.Common;
using Backend.Infrastructure.BackgroundJobs;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api.Configuration;

/// <summary>
/// Database configuration for multi-DB support (PostgreSQL + MySQL)
/// </summary>
public static class DatabaseConfiguration
{
    public static IServiceCollection AddDatabaseConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("krafterDb")
            ?? throw new InvalidOperationException("Database connection string 'krafterDb' not found");

        // TenantDbContext (root tenant registry)
        services.AddDbContext<TenantDbContext>(opts => ConfigureDbContext(opts, connectionString));

        // BackgroundJobsContext (TickerQ operational store)
        services.AddDbContext<BackgroundJobsContext>(opts => ConfigureDbContext(opts, connectionString));

        // KrafterContext (main multi-tenant context)
        services.AddDbContext<KrafterContext>(opts => ConfigureDbContext(opts, connectionString));

        return services;
    }

    private static void ConfigureDbContext(DbContextOptionsBuilder opts, string connectionString)
    {
        switch (DatabaseSelected.Type)
        {
            case DatabaseType.Postgresql:
                opts.UseNpgsql(connectionString);
                break;

            default:
                throw new NotSupportedException($"Database type '{DatabaseSelected.Type}' is not supported");
        }
    }
}
