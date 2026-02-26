using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Common.Interfaces.Auth;
using AditiKraft.Krafter.Backend.Entities;
using AditiKraft.Krafter.Backend.Features.Auth._Shared;
using AditiKraft.Krafter.Backend.Features.Roles._Shared;
using AditiKraft.Krafter.Backend.Features.Users._Shared;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AditiKraft.Krafter.Backend.Infrastructure.Persistence;

public class KrafterContext(
    DbContextOptions<KrafterContext> options,
    ICurrentUser currentUser,
    ITenantGetterService tenantGetterService)
    : IdentityDbContext<KrafterUser, KrafterRole, string, KrafterUserClaim, KrafterUserRole,
        KrafterUserLogin, KrafterRoleClaim, KrafterUserToken>(options)
{
#pragma warning disable CS0108, CS0114
    public virtual DbSet<KrafterUser> Users { get; set; }
#pragma warning restore CS0108, CS0114
    public virtual DbSet<UserRefreshToken> UserRefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<KrafterRole>(entity =>
        {
            entity.Property(c => c.Id).HasMaxLength(36);
            entity.Property(c => c.CreatedById).HasMaxLength(36);
            entity
                .HasIndex(r => new { r.NormalizedName, r.TenantId })
                .IsUnique();

            entity.HasQueryFilter(c => c.IsDeleted == false && c.TenantId == tenantGetterService.Tenant.Id);

            entity.HasMany(e => e.UserRoles)
                .WithOne(e => e.Role)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KrafterRoleClaim>(entity =>
        {
            entity.Property(c => c.Id).HasMaxLength(36);
            entity.Property(c => c.CreatedById).HasMaxLength(36);
            entity.HasQueryFilter(c => c.IsDeleted == false && c.TenantId == tenantGetterService.Tenant.Id);
        });

        modelBuilder.Entity<KrafterUser>(entity =>
        {
            entity.Property(c => c.Id).HasMaxLength(36);
            entity.Property(c => c.CreatedById).HasMaxLength(36);

            entity.HasQueryFilter(c => c.IsDeleted == false && c.TenantId == tenantGetterService.Tenant.Id);

            entity.HasMany(e => e.UserRoles)
                .WithOne(e => e.User)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.NormalizedEmail).IsUnique(false);
            entity.HasIndex(e => new { e.NormalizedEmail, e.TenantId }).IsUnique();

            entity.HasIndex(e => e.NormalizedUserName).IsUnique(false);
            entity.HasIndex(e => new { e.NormalizedUserName, e.TenantId }).IsUnique();
        });

        modelBuilder.Entity<KrafterUserClaim>(entity =>
        {
            entity.Property(c => c.Id).HasMaxLength(36);
            entity.Property(c => c.CreatedById).HasMaxLength(36);
            entity.HasQueryFilter(c => c.IsDeleted == false && c.TenantId == tenantGetterService.Tenant.Id);
        });

        modelBuilder.Entity<KrafterUserRole>(entity =>
        {
            // Configure the relationship with Role
            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure the relationship with User
            entity.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(c => c.CreatedById).HasMaxLength(36);
            entity.HasQueryFilter(c => c.IsDeleted == false && c.TenantId == tenantGetterService.Tenant.Id);
        });

        modelBuilder.Entity<KrafterUserLogin>(entity =>
        {
            // Configure primary keys for custom user login and token classes
            entity
                .HasKey(l => new { l.LoginProvider, l.ProviderKey });
            entity.ToTable("KrafterUserLogins", b => b.IsTemporal());
        });

        modelBuilder.Entity<KrafterUserToken>(entity =>
        {
            entity
                .HasKey(t => new { t.UserId, t.LoginProvider, t.Name });
            entity.ToTable("KrafterUserTokens", b => b.IsTemporal());
        });

        modelBuilder.ApplyCommonConfigureAcrossEntity();

        modelBuilder.Entity<UserRefreshToken>(entity => { entity.HasKey(c => c.UserId); });
    }

    public override int SaveChanges()
    {
        UpdateSoftDeleteStatusesAndSetTenant(null);
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        UpdateSoftDeleteStatusesAndSetTenant(null);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void UpdateSoftDeleteStatusesAndSetTenant(int? userId)
    {
        foreach (EntityEntry entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                case EntityState.Modified:
                    SetTenantAndHistoryInfo(entry);
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.CurrentValues["IsDeleted"] = true;
                    SetTenantAndHistoryInfo(entry);
                    break;
            }
        }
    }

    private void SetTenantAndHistoryInfo(EntityEntry entry)
    {
        if (entry.Entity is IHistory)
        {
            entry.CurrentValues["CreatedOn"] = DateTime.UtcNow;
            entry.Property("CreatedOn").IsModified = true;
            if (currentUser is not null && !string.IsNullOrWhiteSpace(currentUser.GetUserId()))
            {
                entry.CurrentValues["CreatedById"] = currentUser.GetUserId();
                entry.Property("CreatedById").IsModified = true;
            }
        }

        if (entry.Entity is ITenant)
        {
            CurrentTenantDetails tenant = tenantGetterService.Tenant;
            entry.CurrentValues["TenantId"] = tenant.Id;
        }
    }

    public Task<int> SaveChangesAsync(List<string> entitiesToUpdateVersions, bool acceptAllChangesOnSuccess = true,
        CancellationToken cancellationToken = default)
    {
        UpdateSoftDeleteStatusesAndSetTenant(null);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
