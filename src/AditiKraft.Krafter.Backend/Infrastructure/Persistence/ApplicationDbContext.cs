using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Entities;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AditiKraft.Krafter.Backend.Infrastructure.Persistence;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUser currentUser,
    ITenantGetterService tenantGetterService)
    : IdentityDbContext<ApplicationUser, ApplicationRole, string, ApplicationUserClaim, ApplicationUserRole,
        ApplicationUserLogin, ApplicationRoleClaim, ApplicationUserToken>(options)
{
#pragma warning disable CS0108, CS0114
    public virtual DbSet<ApplicationUser> Users { get; set; }
#pragma warning restore CS0108, CS0114
    public virtual DbSet<UserRefreshToken> UserRefreshTokens { get; set; }

    public DbSet<TwoFactorCredential> TwoFactorCredentials => Set<TwoFactorCredential>();
    public DbSet<TwoFactorChallenge> TwoFactorChallenges => Set<TwoFactorChallenge>();
    public DbSet<TwoFactorRecoveryCode> TwoFactorRecoveryCodes => Set<TwoFactorRecoveryCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationRole>(entity =>
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

        modelBuilder.Entity<ApplicationRoleClaim>(entity =>
        {
            entity.Property(c => c.Id).HasMaxLength(36);
            entity.Property(c => c.CreatedById).HasMaxLength(36);
            entity.HasQueryFilter(c => c.IsDeleted == false && c.TenantId == tenantGetterService.Tenant.Id);
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
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

        modelBuilder.Entity<ApplicationUserClaim>(entity =>
        {
            entity.Property(c => c.Id).HasMaxLength(36);
            entity.Property(c => c.CreatedById).HasMaxLength(36);
            entity.HasQueryFilter(c => c.IsDeleted == false && c.TenantId == tenantGetterService.Tenant.Id);
        });

        modelBuilder.Entity<ApplicationUserRole>(entity =>
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

        modelBuilder.Entity<ApplicationUserLogin>(entity =>
        {
            // Configure primary keys for custom user login and token classes
            entity
                .HasKey(l => new { l.LoginProvider, l.ProviderKey });
        });

        modelBuilder.Entity<ApplicationUserToken>(entity =>
        {
            entity
                .HasKey(t => new { t.UserId, t.LoginProvider, t.Name });
        });

        modelBuilder.Entity<TwoFactorCredential>(entity =>
        {
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasMaxLength(36);
            entity.HasQueryFilter(x => !x.IsDeleted && x.TenantId == tenantGetterService.Tenant.Id);
            entity.HasOne<ApplicationUser>().WithOne().HasForeignKey<TwoFactorCredential>(x => x.UserId);
        });
        modelBuilder.Entity<TwoFactorChallenge>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(64);
            entity.Property(x => x.UserId).HasMaxLength(36);
            entity.Property(x => x.Purpose).HasMaxLength(16);
            entity.Property(x => x.PrimaryMethod).HasMaxLength(16);
            entity.HasIndex(x => new { x.TenantId, x.UserId });
            entity.HasIndex(x => x.ExpiresAt);
            entity.HasQueryFilter(x => !x.IsDeleted && x.TenantId == tenantGetterService.Tenant.Id);
        });
        modelBuilder.Entity<TwoFactorRecoveryCode>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(36);
            entity.Property(x => x.UserId).HasMaxLength(36);
            entity.Property(x => x.CodeHash).HasMaxLength(64);
            entity.HasIndex(x => new { x.TenantId, x.UserId });
            entity.HasQueryFilter(x => !x.IsDeleted && x.TenantId == tenantGetterService.Tenant.Id);
        });
        modelBuilder.ApplyCommonConfigureAcrossEntity();

        modelBuilder.Entity<UserRefreshToken>(entity => { entity.HasKey(c => c.UserId); });
    }

    public override int SaveChanges() => SaveChanges(acceptAllChangesOnSuccess: true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateSoftDeleteStatusesAndSetTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        UpdateSoftDeleteStatusesAndSetTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void UpdateSoftDeleteStatusesAndSetTenant()
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
            string? actorId = currentUser?.GetUserId();
            if (entry.State == EntityState.Added)
            {
                entry.CurrentValues[nameof(IHistory.CreatedOn)] = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(actorId))
                {
                    entry.CurrentValues[nameof(IHistory.CreatedById)] = actorId;
                }
            }
            else
            {
                // Identity can mark the entire entity as modified. Creation history must
                // stay out of the UPDATE even when callers supply different values.
                entry.Property(nameof(IHistory.CreatedOn)).IsModified = false;
                entry.Property(nameof(IHistory.CreatedById)).IsModified = false;
                entry.CurrentValues[nameof(IHistory.UpdatedOn)] = DateTime.UtcNow;
                entry.Property(nameof(IHistory.UpdatedOn)).IsModified = true;
                entry.CurrentValues[nameof(IHistory.UpdatedById)] =
                    string.IsNullOrWhiteSpace(actorId) ? null : actorId;
                entry.Property(nameof(IHistory.UpdatedById)).IsModified = true;
            }
        }

        if (entry.Entity is ITenant)
        {
            CurrentTenantDetails tenant = tenantGetterService.Tenant;
            entry.CurrentValues["TenantId"] = tenant.Id;
        }
    }
}
