# Roles Feature AI Instructions

> **SCOPE**: Role CRUD, permission claims, and permission-edit flows.
> **PARENT**: See also: ../../AGENTS.md

## 1. Core Principles
- Use `RoleManager<ApplicationRole>` for role lifecycle operations.
- Treat permission claims as `AppClaimTypes.Permission` entries on `ApplicationRoleClaim`.
- Admin role permissions are protected; do not allow edits for `RoleConstants.Admin`.
- Validate requested permissions against `PermissionCatalog` before saving role metadata. Root-only permissions may be assigned only in the root tenant; backend authorization also enforces this boundary independently of stored claims.
- Permission-bearing reads require `RoleClaims.View`; the dedicated writer requires `RoleClaims.Update`. Combined create/update retain their `Roles` policy and also check the actor's live `RoleClaims.Update` authority before any write when `Permissions` is nonnull. Cached display claims are not authority.
- Deleted role names remain reserved within their tenant, ignoring case. Preflight queries include deleted history and explicitly constrain `TenantId`; known concurrent unique-index conflicts return 409.
- Soft deletion rotates the role's `ConcurrencyStamp` in the same save as the role and claim deletion. Detach a rejected Identity update so a later save cannot restore stale role state.
- Keep route parameter names aligned with the placeholders used by the endpoint.

## 2. Decision Tree
- Creating a role? Follow `CreateRole`.
- Updating role metadata? Follow `UpdateRole`.
- Deleting a role? Follow `DeleteRole` and keep soft-delete behavior intact.
- Listing or loading roles? Follow `GetRoles`, `GetRoleById`, and `GetRoleByIdWithPermissions`.
- Updating permissions? Use `RolePermissionService` from each operation.

## 3. Code Templates

### Shared Permission Synchronization
`CreateRole`, `UpdateRole`, and `UpdateRolePermissions` use `RolePermissionService.SynchronizeAsync`, check its `Response`, then save their `ApplicationDbContext`. Create and combined update first call `ValidatePermissionsAsync`, and keep Identity's metadata save plus claim synchronization in `DomainTransaction`. The synchronizer independently rechecks live claim-update authority, catalog/root restrictions, current-tenant role existence and Admin protection. A failed second check rolls the relational operation back.

The service changes only permission claims for the current tenant and role. An empty list clears permissions and requires claim authority. Re-selected claims are restored from soft delete, and duplicate values are stored once. Omitted/null `Permissions` in create/update means metadata-only and needs no claim authority.

### Protect Admin Role Permissions
```csharp
if (role.Name == RoleConstants.Admin)
{
    return Response.BadRequest("Not allowed to modify Permissions for this Role.");
}
```

## 4. Checklist
1. Use `ApiRoutes.Roles` for all role endpoints.
2. Use `RoleManager<ApplicationRole>` for role lookup and creation.
3. Store permission claims with `ClaimType = AppClaimTypes.Permission`.
4. Keep admin-role permission protection intact.
5. Keep empty-list clearing, soft-delete restoration, and tenant boundaries intact.

## 5. Common Mistakes
- Allowing permission edits for the Admin role.
- Mixing route parameter names (`roleId`, `id`) incorrectly between route templates and handler parameters.
- Hard-deleting permission claims instead of matching the existing slice behavior.
- Duplicating permission synchronization in operation handlers.

## 6. Evolution Triggers
- Role permission storage changes.
- Admin/basic role protection rules change.
- Role read/write flows split into additional slices.

## References (real code)
- `src/AditiKraft.Krafter.Backend/Features/Roles/Common/RolePermissionService.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/CreateRole.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/GetRoles.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/GetRoleById.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/GetRoleByIdWithPermissions.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/UpdateRole.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/UpdateRolePermissions.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/DeleteRole.cs`

---
Last Updated: 2026-11-08
Verified Against: src/AditiKraft.Krafter.Backend/Features/Roles/Common/RolePermissionService.cs, src/AditiKraft.Krafter.Backend/Features/Roles/CreateRole.cs, src/AditiKraft.Krafter.Backend/Features/Roles/GetRoles.cs, src/AditiKraft.Krafter.Backend/Features/Roles/GetRoleById.cs, src/AditiKraft.Krafter.Backend/Features/Roles/GetRoleByIdWithPermissions.cs, src/AditiKraft.Krafter.Backend/Features/Roles/UpdateRole.cs, src/AditiKraft.Krafter.Backend/Features/Roles/UpdateRolePermissions.cs, src/AditiKraft.Krafter.Backend/Features/Roles/DeleteRole.cs, src/AditiKraft.Krafter.Backend/Features/Roles/Common/RolePermissionService.cs, src/AditiKraft.Krafter.Contracts/Common/Auth/AppClaimTypes.cs, src/AditiKraft.Krafter.Contracts/Contracts/Roles/RoleConstants.cs, src/AditiKraft.Krafter.Contracts/Common/ApiRoutes.cs
---
