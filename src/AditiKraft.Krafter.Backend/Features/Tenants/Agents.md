# Tenants Feature AI Instructions

> **SCOPE**: Tenant CRUD, seed data, and tenant admin sync.
> **PARENT**: See also: ../../Agents.md

## 1. Core Principles
- Use `TenantDbContext` for tenant data; use `ApplicationDbContext` for identity-side updates.
- Creating a tenant triggers data seeding in a scoped tenant context.
- `DefaultTenantInitializer.StartAsync` provisions the root identity before either web host accepts requests. The shared `AddBackendServices` registration owns this awaited startup step; the migrator remains responsible for schema migrations.
- Only trusted startup and `CreateTenant.Handler` call `DataSeedService.SeedAsync`. There is no HTTP or UI seeding path. After the tenant registry commits, finish tenant provisioning independently of request cancellation and queue the welcome email after identity commit.
- Provision only tenants with no user or role history. Check both tables with `IgnoreQueryFilters()` and an explicit `TenantId` predicate so soft-deleted and partial identity state cannot trigger repair or recreate an owner. Existing roles never regain removed permission claims on restart.
- Serialize bootstrap with the PostgreSQL tenant advisory transaction lock; keep all Identity saves, role claims, and memberships atomic. Reject pending tracked changes, clear stale state under the lock, and clear tracked state after rollback.
- For a genuinely empty root outside Development, require `Bootstrap:RootPassword` from private configuration, reject the public Development password, enforce the shared `PasswordLimits.MaximumLength`, and validate through Identity before staging rows. Existing identity history returns before accessing this setting. See the startup guidance in README.md.
- `CreateTenant.Handler` sets `CreatedOn` to UTC before saving. `TenantDbContext` does not set audit timestamps. Updates must preserve the creation time.
- `ValidUpto` is an exact UTC expiry timestamp. The UI converts browser-local input to UTC before sending it. Validate UTC in backend handlers, after shared form validation, and preserve the time on create and update; truncating it to midnight changes the expiry. Preserve the root tenant's protected validity value.
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
- Provision root identity? Use the awaited `DefaultTenantInitializer` in shared backend startup.
- Provision new tenant identity? Use `DataSeedService.SeedAsync` from `CreateTenant` after committing the registry entry; never expose it as an HTTP endpoint.

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
// The tenant registry has committed, so finish mandatory provisioning after a disconnect.
await seedService.SeedAsync(CancellationToken.None);
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
- `src/AditiKraft.Krafter.Backend/Features/Tenants/Common/DefaultTenantInitializer.cs`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/Common/DataSeedService.cs`

---
Last Updated: 2026-11-08
Verified Against: README.md, docs/url-configuration.md, src/AditiKraft.Krafter.Contracts/Common/AppUrls.cs, src/AditiKraft.Krafter.Contracts/Contracts/Tenants/CreateOrUpdateTenantRequest.cs, tests/AditiKraft.Krafter.Tests/Tenants/TenantCreationTests.cs, tests/AditiKraft.Krafter.Tests/Tenants/DataSeedServiceTests.cs, tests/AditiKraft.Krafter.Tests/Tenants/DataSeedPostgresTests.cs, tests/AditiKraft.Krafter.Tests/Tenants/DefaultTenantStartupTests.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/CreateTenant.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/UpdateTenant.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/GetTenants.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/DeleteTenant.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/Common/DefaultTenantInitializer.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/Common/DataSeedService.cs, src/AditiKraft.Krafter.Backend/Web/HostingExtensions.cs, src/AditiKraft.Krafter.Backend/Features/Users/Common/UserService.cs
---



