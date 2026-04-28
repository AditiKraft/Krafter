using AditiKraft.Krafter.Backend.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Web.Configuration;

public static class DatabaseConfiguration
{
    public static IServiceCollection AddDatabaseConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("appDb")
                                  ?? throw new InvalidOperationException(
                                      "Database connection string 'appDb' not found");

        // TenantDbContext (root tenant registry)
        services.AddDbContext<TenantDbContext>(opts => ConfigureDbContext(opts, connectionString));

        // BackgroundJobsContext (TickerQ operational store)
        services.AddDbContext<BackgroundJobsContext>(opts => ConfigureDbContext(opts, connectionString));

        // KrafterContext (main multi-tenant context)
        services.AddDbContext<ApplicationDbContext>(opts => ConfigureDbContext(opts, connectionString));

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


