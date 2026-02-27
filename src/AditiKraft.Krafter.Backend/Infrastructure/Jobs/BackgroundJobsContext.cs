using AditiKraft.Krafter.Backend.Common;
using Microsoft.EntityFrameworkCore;
using TickerQ.EntityFrameworkCore.Configurations;
using TickerQ.EntityFrameworkCore.Entities;

namespace AditiKraft.Krafter.Backend.Infrastructure.Jobs;

public class BackgroundJobsContext : DbContext
{
    public BackgroundJobsContext(DbContextOptions<BackgroundJobsContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply TickerQ entity configurations explicitly
        builder.ApplyConfiguration(new TimeTickerConfigurations());
        builder.ApplyConfiguration(new CronTickerConfigurations());
        builder.ApplyConfiguration(new CronTickerOccurrenceConfigurations());
        // Configure GUID columns for MySQL compatibility
        // Override GUID column configuration for MySQL compatibility
        if (DatabaseSelected.Type == DatabaseType.MySql)
        {
            // Configure TimeTickerEntity GUID columns
            builder.Entity<TimeTickerEntity>(entity =>
            {
                entity.Property(e => e.Id)
                    .HasColumnType("char(36)")
                    .HasAnnotation("Relational:Collation", "utf8mb4_bin");

                entity.Property(e => e.BatchParent)
                    .HasColumnType("char(36)")
                    .HasAnnotation("Relational:Collation", "utf8mb4_bin");
            });

            // Configure CronTickerEntity GUID columns
            builder.Entity<CronTickerEntity>(entity =>
            {
                entity.Property(e => e.Id)
                    .HasColumnType("char(36)")
                    .HasAnnotation("Relational:Collation", "utf8mb4_bin");
            });

            // Configure CronTickerOccurrenceEntity GUID columns
            builder.Entity<CronTickerOccurrenceEntity<CronTickerEntity>>(entity =>
            {
                entity.Property(e => e.Id)
                    .HasColumnType("char(36)")
                    .HasAnnotation("Relational:Collation", "utf8mb4_bin");

                entity.Property(e => e.CronTickerId)
                    .HasColumnType("char(36)")
                    .HasAnnotation("Relational:Collation", "utf8mb4_bin");
            });
        }
        // Alternatively, apply all configurations from assembly:
        // builder.ApplyConfigurationsFromAssembly(typeof(TimeTickerConfigurations).Assembly);
    }
}

