# Persistence AI Instructions (EF Core)

> **SCOPE**: DbContext usage, model configuration, query filters, migrations.
> **PARENT**: See also: ../../Agents.md

## 1. Core Principles
- Use `KrafterContext` for tenant-scoped data; use `TenantDbContext` for tenant admin data.
- Tenant-scoped entities require query filters on `TenantId` + `IsDeleted`.
- `KrafterContext` uses soft delete; do not remove entities directly.
- Keep `ApplyCommonConfigureAcrossEntity()` in `OnModelCreating`.
- Runtime migration execution belongs in `src/AditiKraft.Krafter.Backend.Migrator/`; `aspire/` should orchestrate it as an executable resource and gate the API with `WaitForCompletion(...)`.
- `dotnet ef migrations add` only needs `ConnectionStrings:KrafterDbMigration` to exist in `src/AditiKraft.Krafter.Backend/appsettings.Local.json`; the existing placeholder value is enough.

## 2. Decision Tree
- New tenant-scoped entity? Add to `KrafterContext` and apply tenant query filter.
- Tenant management or cross-tenant queries? Use `TenantDbContext`.
- Provider-specific history/temporal rules? Update `ModelBuilderExtensions`.

## 3. Code Templates

### Tenant Query Filter (KrafterContext)
```csharp
modelBuilder.Entity<KrafterUser>(entity =>
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
            Id = KrafterInitialConstants.RootTenant.Id,
            Identifier = KrafterInitialConstants.RootTenant.Identifier,
            Name = KrafterInitialConstants.RootTenant.Name,
            AdminEmail = KrafterInitialConstants.RootUser.EmailAddress
        }
    });
});
```

## 4. Checklist
1. Add entity in `src/AditiKraft.Krafter.Backend/Features/<Feature>/Common/<Entity>.cs`.
2. Add a `DbSet` in `src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/KrafterContext.cs`.
3. Configure model in `KrafterContext.OnModelCreating(...)`.
4. Use `TenantDbContext` only for tenant admin data.
5. Create the migration in `src/AditiKraft.Krafter.Backend/Migrations`.
6. Leave `src/AditiKraft.Krafter.Backend/appsettings.Local.json` in place for `dotnet ef migrations add`.
7. Let `src/AditiKraft.Krafter.Backend.Migrator/` apply migrations at startup; do not run them from the API host.

## 5. Common Mistakes
- Missing tenant query filter for tenant-scoped entities.
- Using `TenantDbContext` for tenant-scoped data (or vice versa).
- Forgetting to add a `DbSet` to `KrafterContext`.
- Removing entities instead of relying on soft delete.
- Forgetting to add a migration after changing the model, which causes `PendingModelChangesWarning` in the migrator.

## 6. Evolution Triggers
- New database provider added.
- Changes to `KrafterContext` soft-delete behavior.
- New cross-entity configuration added in `ModelBuilderExtensions`.

---
Last Updated: 2026-03-07
Verified Against: src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/KrafterContext.cs, src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/TenantDbContext.cs, src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/ModelBuilderExtensions.cs, src/AditiKraft.Krafter.Backend.Migrator/Program.cs, src/AditiKraft.Krafter.Backend.Migrator/ApiDbInitializer.cs, aspire/AditiKraft.Krafter.Aspire.AppHost/Program.cs
---


