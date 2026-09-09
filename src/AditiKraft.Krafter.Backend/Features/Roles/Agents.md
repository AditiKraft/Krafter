# Roles Feature AI Instructions

> **SCOPE**: Role CRUD, permission claims, and permission-edit flows.
> **PARENT**: See also: ../../Agents.md

## 1. Core Principles
- Use `RoleManager<ApplicationRole>` for role lifecycle operations.
- Treat permission claims as `AppClaimTypes.Permission` entries on `ApplicationRoleClaim`.
- Admin role permissions are protected; do not allow edits for `RoleConstants.Admin`.
- Keep route parameter names aligned with the placeholders used by the endpoint.

## 2. Decision Tree
- Creating a role? Follow `CreateRole`.
- Updating role metadata? Follow `UpdateRole`.
- Deleting a role? Follow `DeleteRole` and keep soft-delete behavior intact.
- Listing or loading roles? Follow `GetRoles`, `GetRoleById`, and `GetRoleByIdWithPermissions`.
- Updating permissions? Use `RolePermissionService` from each operation.

## 3. Code Templates

### Shared Permission Synchronization
`CreateRole`, `UpdateRole`, and `UpdateRolePermissions` use `RolePermissionService.SynchronizeAsync`, then save their `ApplicationDbContext`.

The service changes only permission claims for the current tenant and role. An empty list clears permissions. Re-selected claims are restored from soft delete, and duplicate values are stored once. A null list in `UpdateRole` leaves permissions unchanged.

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
- `src/AditiKraft.Krafter.Backend/Features/Roles/Common/RoleService.cs`

---
Last Updated: 2026-09-09
Verified Against: src/AditiKraft.Krafter.Backend/Features/Roles/Common/RolePermissionService.cs, src/AditiKraft.Krafter.Backend/Features/Roles/CreateRole.cs, src/AditiKraft.Krafter.Backend/Features/Roles/GetRoles.cs, src/AditiKraft.Krafter.Backend/Features/Roles/GetRoleById.cs, src/AditiKraft.Krafter.Backend/Features/Roles/GetRoleByIdWithPermissions.cs, src/AditiKraft.Krafter.Backend/Features/Roles/UpdateRole.cs, src/AditiKraft.Krafter.Backend/Features/Roles/UpdateRolePermissions.cs, src/AditiKraft.Krafter.Backend/Features/Roles/DeleteRole.cs, src/AditiKraft.Krafter.Backend/Features/Roles/Common/RoleService.cs, src/AditiKraft.Krafter.Contracts/Common/Auth/AppClaimTypes.cs, src/AditiKraft.Krafter.Contracts/Contracts/Roles/RoleConstants.cs, src/AditiKraft.Krafter.Contracts/Common/ApiRoutes.cs
---
