# Tenants Feature AI Instructions

> **SCOPE**: Tenant CRUD, seed data, and tenant admin sync.
> **PARENT**: See also: ../../AGENTS.md

## 1. Core Principles
- Use `TenantDbContext` for tenant data; use `ApplicationDbContext` for identity-side updates.
- Creating a tenant stages identity provisioning in a scoped tenant context; registry, roles, claims, owner and memberships commit in one `DomainTransaction`. PostgreSQL is the supported provider; InMemory tests do not establish rollback guarantees.
- `DefaultTenantInitializer.StartAsync` provisions the root identity before either web host accepts requests. The shared `AddBackendServices` registration owns this awaited startup step; the migrator remains responsible for schema migrations.
- Trusted startup calls `DataSeedService.SeedAsync`; `CreateTenant.Handler` calls its internal `ProvisionAsync` within the shared transaction. There is no HTTP or UI seeding path. Check cancellation before commit and complete commit independently once started; queue the returned welcome email only after the entire transaction commits. Job persistence is a separate durable boundary, so queue failures must not be described as identity rollback.
- Provision only tenants with no user or role history. Check both tables with `IgnoreQueryFilters()` and an explicit `TenantId` predicate so soft-deleted and partial identity state cannot trigger repair or recreate an owner. Existing roles never regain removed permission claims on restart.
- Serialize bootstrap with the PostgreSQL tenant advisory transaction lock; keep all Identity saves, role claims, and memberships atomic. Reject pending tracked changes, clear stale state under the lock, and clear tracked state after rollback.
- For a genuinely empty root outside Development, require `Bootstrap:RootPassword` from private configuration, reject the public Development password, enforce the shared `PasswordLimits.MaximumLength`, and validate through Identity before staging rows. Existing identity history returns before accessing this setting. See the startup guidance in README.md.
- `CreateTenant.Handler` sets `CreatedOn` to UTC before saving. `TenantDbContext` does not set audit timestamps. Updates must preserve the creation time.
- `ValidUpto` is an exact UTC expiry timestamp. The UI converts browser-local input to UTC before sending it. Validate UTC in backend handlers, after shared form validation, and preserve the time on create and update; truncating it to midnight changes the expiry. Preserve the root tenant's protected validity value.
- Root tenant cannot be deleted.
- Validate identifiers through `CreateOrUpdateTenantRequestValidator` with the configured `AppUrls` in both create and update handlers. Keep format, length, and reserved-name rules shared with the UI. Check uniqueness without regard to letter case, excluding the current tenant on update.
- `IX_Tenant_Identifier_Lower` enforces uniqueness for tenants that are not deleted. Its expression index is defined in the `UniqueTenantIdentifiers` raw SQL migration, not the EF model snapshot. Return 409 for this named constraint violation if concurrent writes pass the initial check.
- Build tenant links with `TenantLinkBuilder.GetTenantLink(urls, identifier)`. Read [Configure application URLs](../../../../docs/url-configuration.md) before changing tenant domains or reserved names.
- Validate owner email length, normalized identity limits, and the configured Identity username alphabet before registry writes. Creation also runs configured user validators before staging the tenant. Shared request validation caps admin email at 256 characters, matching Identity storage.
- Update admin emails through `IUserMutationService.UpdateEmailAsync` in the target tenant scope, enlisted with the registry on one connection/transaction. Resolve the single `IsOwner` account by ID; missing or ambiguous ownership returns 409 instead of silently skipping synchronization. Check the response before saving the registry; profile fields and roles remain intact.
- Carry the initiating actor ID through `ICurrentUserInitializer.SetCurrentUserId` in fresh tenant identity scopes for audit attribution. The outer tenant route still owns authorization; selecting a child tenant does not grant permissions.

## 2. Decision Tree
- Create tenant? Use `CreateTenant` (checks identifier uniqueness and seeds data).
- Update tenant? Use `UpdateTenant` (handles tenant updates and admin sync).
- Get tenants? Use `Get` with `GetRequestInput` (supports current and deleted tenants; history requests return BadRequest).
- Delete tenant? Use `DeleteTenant` with `RouteSegment.ById`.
- Provision root identity? Use the awaited `DefaultTenantInitializer` in shared backend startup.
- Provision new tenant identity? Use `DataSeedService.ProvisionAsync` from `CreateTenant` inside the shared registry/identity transaction, then `QueueWelcomeAsync` after commit; never expose provisioning as an HTTP endpoint.

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

### Provision New Tenant Data

`CreateTenant` sets the target tenant and initiating audit actor in a fresh scope, then calls `ValidateOwnerAsync` before writes. The registry context owns `DomainTransaction`; the target `ApplicationDbContext` borrows its connection and transaction. Inside that boundary, save the tenant and await `ProvisionAsync`. Commit, dispose the transaction, then call `QueueWelcomeAsync` with the returned email. See the operation for the complete sequence and the named identifier-conflict mapping.

## 4. Checklist
1. Use `TenantDbContext` for tenant queries and writes.
2. Persist related updates in `ApplicationDbContext` when needed.
3. Prevent root tenant deletion (`SeedDataConstants.RootTenant.Id`).
4. Commit new tenant registration and identity provisioning atomically.

## 5. Common Mistakes
- Skipping identifier uniqueness check.
- Committing a tenant before its mandatory identity provisioning succeeds.
- Looking up the owner by an old registry email or silently ignoring missing ownership.
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



