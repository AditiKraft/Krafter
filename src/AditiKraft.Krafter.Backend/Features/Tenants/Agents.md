# Tenants Feature AI Instructions

> **SCOPE**: Tenant CRUD, seed data, and tenant admin sync.
> **PARENT**: See also: ../../Agents.md

## 1. Core Principles
- Use `TenantDbContext` for tenant data; use `KrafterContext` for identity-side updates.
- Creating a tenant triggers data seeding in a scoped tenant context.
- Root tenant cannot be deleted.

## 2. Decision Tree
- Create/update tenant? Use `CreateOrUpdate` (checks identifier uniqueness).
- Get tenants? Use `Get` with `GetRequestInput` (supports history and deleted).
- Delete tenant? Use `Delete` with `RouteSegment.ById`.
- Seed tenant data? Use `SeedBasicData` route with `RouteSegment.SeedData`.

## 3. Code Templates

### Ensure Unique Identifier
```csharp
Tenant? existingTenant = await dbContext.Tenants
    .AsNoTracking()
    .FirstOrDefaultAsync(c => c.Identifier.ToLower() == request.Identifier.ToLower());

if (existingTenant is not null)
{
    return Response.Conflict("Identifier already exists, please try a different identifier.");
}
```

### Seed New Tenant Data
```csharp
using (IServiceScope scope = serviceProvider.CreateScope())
{
    ITenantSetterService setter = scope.ServiceProvider.GetRequiredService<ITenantSetterService>();
    CurrentTenantDetails currentTenant = entity.Adapt<CurrentTenantDetails>();
    currentTenant.TenantLink = GetSubTenantLinkBasedOnRootTenant(rootTenantLink, request.Identifier);
    setter.SetTenant(currentTenant);

    DataSeedService seedService = scope.ServiceProvider.GetRequiredService<DataSeedService>();
    await seedService.SeedBasicData(new SeedDataRequest());
}
```

## 4. Checklist
1. Use `TenantDbContext` for tenant queries and writes.
2. Persist related updates in `KrafterContext` when needed.
3. Prevent root tenant deletion (`KrafterInitialConstants.RootTenant.Id`).
4. Seed new tenant data after creation.

## 5. Common Mistakes
- Skipping identifier uniqueness check.
- Failing to seed tenant data after create.
- Deleting the root tenant.

## 6. Evolution Triggers
- Tenant link calculation changes.
- Seed data flow changes.
- Tenant admin email sync changes.

## References (real code)
- `src/AditiKraft.Krafter.Backend/Features/Tenants/CreateOrUpdate.cs`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/Get.cs`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/Delete.cs`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/SeedBasicData.cs`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/_Shared/DataSeedService.cs`

---
Last Updated: 2026-01-25
Verified Against: Features/Tenants/CreateOrUpdate.cs, Features/Tenants/Get.cs, Features/Tenants/Delete.cs, Features/Tenants/SeedBasicData.cs, Features/Tenants/_Shared/DataSeedService.cs
---
