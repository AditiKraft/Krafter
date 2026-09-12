using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Infrastructure.Persistence;

public class TenantDbContext(DbContextOptions<TenantDbContext> options)
    : DbContext(options)
{
    // The expression index is created by UniqueTenantIdentifiers; EF indexes only model properties.
    public const string TenantIdentifierIndexName = "IX_Tenant_Identifier_Lower";

    public DbSet<Tenant> Tenants { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasQueryFilter(c => c.IsDeleted == false);

            entity.ToTable(nameof(Tenant));

            entity.HasData(new List<Tenant>
            {
                new()
                {
                    Id = SeedDataConstants.RootTenant.Id,
                    Identifier = SeedDataConstants.RootTenant.Identifier,
                    IsActive = true,
                    Name = SeedDataConstants.RootTenant.Name,
                    CreatedOn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), // ✅ FIXED
                    ValidUpto = DateTime.MaxValue,
                    AdminEmail = SeedDataConstants.RootUser.EmailAddress
                }
            });
        });
    }
}
