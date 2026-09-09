using AditiKraft.Krafter.Backend.Common.Entities;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AditiKraft.Krafter.Backend.Infrastructure.Persistence;

public static class ModelBuilderExtensions
{
    public static void ApplyCommonConfigureAcrossEntity(this ModelBuilder builder)
    {
        var allEntities = builder.Model.GetEntityTypes().ToList();

        IEnumerable<IMutableEntityType> tenantEntities = allEntities
            .Where(e => typeof(ITenant).IsAssignableFrom(e.ClrType));
        foreach (IMutableEntityType entityType in tenantEntities)
        {
            builder.Entity(entityType.ClrType).Property("TenantId").IsRequired();
            builder.Entity(entityType.ClrType).Property("TenantId").HasMaxLength(36);
        }

        IEnumerable<IMutableEntityType> mutableEntityTypes = allEntities
            .Where(e => typeof(ICommonEntityProperty).IsAssignableFrom(e.ClrType));
        foreach (IMutableEntityType entityType in mutableEntityTypes)
        {
            builder.Entity(entityType.ClrType).Property("Id").HasMaxLength(36);
            builder.Entity(entityType.ClrType).Property("CreatedById").HasMaxLength(36);
        }

        IEnumerable<IMutableEntityType> historyEntities = allEntities
            .Where(e => typeof(IHistory).IsAssignableFrom(e.ClrType));
        foreach (IMutableEntityType entityType in historyEntities)
        {
            builder.Entity(entityType.ClrType).ToTable(entityType.ClrType.Name);
            builder.Entity(entityType.ClrType).Property("CreatedOn")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }

        Type commonEntityType = typeof(ICommonAuthEntityProperty);
        foreach (IMutableEntityType entityType in builder.Model.GetEntityTypes())
        {
            if (commonEntityType.IsAssignableFrom(entityType.ClrType) && entityType.ClrType != commonEntityType)
            {
                builder.Entity(entityType.ClrType).HasOne(typeof(ApplicationUser), "CreatedBy")
                    .WithMany()
                    .HasForeignKey("CreatedById")
                    .OnDelete(DeleteBehavior.Restrict);

                builder.Entity(entityType.ClrType).HasOne(typeof(ApplicationUser), "UpdatedBy")
                    .WithMany()
                    .HasForeignKey("UpdatedById")
                    .OnDelete(DeleteBehavior.Restrict);
            }
        }
    }
}



