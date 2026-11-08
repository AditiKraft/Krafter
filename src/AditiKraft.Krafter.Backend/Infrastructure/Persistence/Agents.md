# Persistence AI Instructions (EF Core)

> **SCOPE**: DbContext usage, model configuration, query filters, migrations.
> **PARENT**: See also: ../../Agents.md

## 1. Core Principles
- Use `ApplicationDbContext` for tenant-scoped data; use `TenantDbContext` for tenant admin data.
- Exception: feature-owned `GoogleLoginTransaction` uses `TenantDbContext` for shared root-to-tenant OAuth orchestration. Its payload is protected; one-use consumption is an atomic conditional SQL delete matching browser proof, tenant and expiry, not a tracked soft delete.
- Tenant-scoped entities require query filters on `TenantId` + `IsDeleted`.
- `ApplicationDbContext` uses soft delete; do not remove entities directly.
- Keep `ApplyCommonConfigureAcrossEntity()` in `OnModelCreating`.
- Runtime migration execution belongs in `src/AditiKraft.Krafter.Backend.Migrator/`; `aspire/` should orchestrate it as an executable resource and gate the API with `WaitForCompletion(...)`.
- For `dotnet ef migrations add`, set `ConnectionStrings__AppDbMigration` or copy `appsettings.Local.json.example` to the ignored `appsettings.Local.json` in the current directory and set `ConnectionStrings:AppDbMigration`. Local settings are excluded from template packages and generated applications. A placeholder PostgreSQL connection string is enough to generate migration files.
- Single-host output excludes backend settings files. Use the environment variable for design-time commands.

## 2. Decision Tree
- New tenant-scoped entity? Add to `ApplicationDbContext` and apply tenant query filter.
- Tenant management or cross-tenant queries? Use `TenantDbContext`.
- PostgreSQL is the supported provider. Tenant history queries are not supported.
- Old migration snapshots contain unused SQL Server annotations. Leave historical migrations unchanged; the PostgreSQL model comparison test verifies that no schema update is required.
- Use the standard `SaveChangesAsync(cancellationToken)` overload; it applies tenant and soft-delete rules.

## 3. Code Templates

### Tenant Query Filter (ApplicationDbContext)
```csharp
modelBuilder.Entity<ApplicationUser>(entity =>
{
    entity.HasQueryFilter(c => c.IsDeleted == false && c.TenantId == tenantGetterService.Tenant.Id);
});
```

### Common Configuration
```csharp
modelBuilder.ApplyCommonConfigureAcrossEntity();
```

### Tenant Seeding (TenantDbContext)
```csharp
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
            Name = SeedDataConstants.RootTenant.Name,
            AdminEmail = SeedDataConstants.RootUser.EmailAddress
        }
    });
});
```

## 4. Checklist
1. Add entity in `src/AditiKraft.Krafter.Backend/Features/<Feature>/Common/<Entity>.cs`.
2. Add a `DbSet` in `src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/ApplicationDbContext.cs`.
3. Configure model in `ApplicationDbContext.OnModelCreating(...)`.
4. Use `TenantDbContext` only for tenant admin data.
5. Create the migration in `src/AditiKraft.Krafter.Backend/Migrations`.
6. Supply `AppDbMigration` through the environment or an optional local settings file for design-time commands.
7. Let `src/AditiKraft.Krafter.Backend.Migrator/` apply migrations at startup; do not run them from the API host.

## 5. Common Mistakes
- Missing tenant query filter for tenant-scoped entities.
- Using `TenantDbContext` for tenant-scoped data (or vice versa).
- Forgetting to add a `DbSet` to `ApplicationDbContext`.
- Removing entities instead of relying on soft delete.
- Forgetting to add a migration after changing the model, which causes `PendingModelChangesWarning` in the migrator.

## 6. Evolution Triggers
- New database provider added.
- Changes to `ApplicationDbContext` soft-delete behavior.
- New cross-entity configuration added in `ModelBuilderExtensions`.

## MFA persistence
- `TwoFactorCredential`, `TwoFactorChallenge` and `TwoFactorRecoveryCode` are tenant-scoped, with explicit query filters. Account transitions acquire a PostgreSQL advisory transaction lock before reloading tracked state.
- Consume challenges/recovery codes by updating state under that lock; do not issue credentials before the transaction succeeds.
- `AuthenticationVersion` and the user concurrency stamp change on factor changes. Access and refresh validation must reject prior versions.
- `TwoFactorCleanup` deliberately uses untracked `ExecuteDeleteAsync` only for expired challenges/consumed recovery rows. Tracked deletes still follow the existing soft-delete rules.
- Real PostgreSQL tests require a dedicated `KRAFTER_TEST_POSTGRES` database. EF InMemory tests do not establish concurrency guarantees.

---
Last Updated: 2026-11-07
Verified Against: src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/ApplicationDbContext.cs, src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/TenantDbContext.cs, src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/ModelBuilderExtensions.cs, src/AditiKraft.Krafter.Backend/Features/Users/Common/UserService.cs, src/AditiKraft.Krafter.Backend.Migrator/Program.cs, src/AditiKraft.Krafter.Backend.Migrator/ApiDbInitializer.cs, aspire/AditiKraft.Krafter.Aspire.AppHost/Program.cs
---


