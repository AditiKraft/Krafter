using AditiKraft.Krafter.Backend.Infrastructure.BackgroundJobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AditiKraft.Krafter.Backend.Infrastructure.Persistence;

internal static class DesignTimeConnectionStringHelper
{
    public static string GetConnectionString()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Local.json", false, false)
            .AddEnvironmentVariables()
            .Build();
        string? connectionString = configuration.GetConnectionString("KrafterDbMigration");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'KrafterDbMigration' not found. Please create 'appsettings.Local.json' with the connection string or set it via environment variables.");
        }

        return connectionString;
    }
}

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        string connectionString = DesignTimeConnectionStringHelper.GetConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure());

        return new TenantDbContext(optionsBuilder.Options);
    }
}

public class DesignTimeKrafterContextDbContextFactory : IDesignTimeDbContextFactory<KrafterContext>
{
    public KrafterContext CreateDbContext(string[] args)
    {
        string connectionString = DesignTimeConnectionStringHelper.GetConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<KrafterContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure());

        return new KrafterContext(optionsBuilder.Options, null, null);
    }
}

public class DesignTimeBackgroundJobsContextDbContextFactory : IDesignTimeDbContextFactory<BackgroundJobsContext>
{
    public BackgroundJobsContext CreateDbContext(string[] args)
    {
        string connectionString = DesignTimeConnectionStringHelper.GetConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<BackgroundJobsContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure());

        return new BackgroundJobsContext(optionsBuilder.Options);
    }
}
