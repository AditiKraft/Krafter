# Tenants Feature AI Instructions

> **SCOPE**: Tenant CRUD, seed data, and tenant admin sync.
> **PARENT**: See also: ../../Agents.md

## 1. Core Principles
- Use `TenantDbContext` for tenant data; use `ApplicationDbContext` for identity-side updates.
- Creating a tenant triggers data seeding in a scoped tenant context.
- `CreateTenant.Handler` sets `CreatedOn` to UTC before saving. `TenantDbContext` does not set audit timestamps. Updates must preserve the creation time.
- Store the selected `ValidUpto` calendar date at midnight UTC on create and update. Date-picker input can have `DateTimeKind.Unspecified`; keep its calendar date instead of converting through the server time zone. Preserve the root tenant's protected validity value.
- Root tenant cannot be deleted.
- Validate identifiers through `CreateOrUpdateTenantRequestValidator` with the configured `AppUrls` in both create and update handlers. Keep format, length, and reserved-name rules shared with the UI. Check uniqueness without regard to letter case, excluding the current tenant on update.
- `IX_Tenant_Identifier_Lower` enforces uniqueness for tenants that are not deleted. Its expression index is defined in the `UniqueTenantIdentifiers` raw SQL migration, not the EF model snapshot. Return 409 for this named constraint violation if concurrent writes pass the initial check.
- Build tenant links with `TenantLinkBuilder.GetTenantLink(urls, identifier)`. Read [Configure application URLs](../../../../docs/url-configuration.md) before changing tenant domains or reserved names.
- Update admin emails through `IUserMutationService.UpdateEmailAsync` in the target tenant scope. Check the response before saving the tenant record; this keeps profile fields and roles intact.

## 2. Decision Tree
- Create tenant? Use `CreateTenant` (checks identifier uniqueness and seeds data).
- Update tenant? Use `UpdateTenant` (handles tenant updates and admin sync).
- Get tenants? Use `Get` with `GetRequestInput` (supports current and deleted tenants; history requests return BadRequest).
- Delete tenant? Use `DeleteTenant` with `RouteSegment.ById`.
- Seed tenant data? Use `SeedBasicData` route with `RouteSegment.SeedData`.

## 3. Code Templates

### Ensure Unique Identifier
```csharp
bool identifierExists = await dbContext.Tenants.AsNoTracking()
    .AnyAsync(c => c.Identifier.ToLower() == request.Identifier, cancellationToken);

if (identifierExists)
{
    return Response.Conflict("Identifier already exists, please try a different identifier.");
}
```

### Seed New Tenant Data
```csharp
using IServiceScope scope = serviceProvider.CreateScope();
ITenantSetterService tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantSetterService>();
CurrentTenantDetails currentTenantDetails = entity.Adapt<CurrentTenantDetails>();
currentTenantDetails.TenantLink =
    TenantLinkBuilder.GetTenantLink(urls, request.Identifier);
tenantSetter.SetTenant(currentTenantDetails);

DataSeedService seedService = scope.ServiceProvider.GetRequiredService<DataSeedService>();
await seedService.SeedBasicData(new SeedDataRequest());
```

## 4. Checklist
1. Use `TenantDbContext` for tenant queries and writes.
2. Persist related updates in `ApplicationDbContext` when needed.
3. Prevent root tenant deletion (`SeedDataConstants.RootTenant.Id`).
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
- `src/AditiKraft.Krafter.Backend/Migrations/TenantDb/20260912000000_UniqueTenantIdentifiers.cs`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/CreateTenant.cs`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/UpdateTenant.cs`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/GetTenants.cs`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/DeleteTenant.cs`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/SeedBasicData.cs`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/Common/DataSeedService.cs`

---
Last Updated: 2026-09-12
Verified Against: docs/url-configuration.md, src/AditiKraft.Krafter.Contracts/Common/AppUrls.cs, src/AditiKraft.Krafter.Contracts/Contracts/Tenants/CreateOrUpdateTenantRequest.cs, tests/AditiKraft.Krafter.Tests/Tenants/TenantCreationTests.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/CreateTenant.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/UpdateTenant.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/GetTenants.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/DeleteTenant.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/SeedBasicData.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/Common/DataSeedService.cs, src/AditiKraft.Krafter.Backend/Features/Users/Common/UserService.cs
---



