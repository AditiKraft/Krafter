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
- Updating permissions? Follow the existing file you are editing and do not introduce a third permission-sync pattern.

## 3. Code Templates

### Permission Claim Sync During Create / Update
```csharp
List<ApplicationRoleClaim> permissions = await db.RoleClaims
    .IgnoreQueryFilters()
    .Where(c => c.TenantId == tenantId &&
                c.RoleId == roleId &&
                c.ClaimType == AppClaimTypes.Permission)
    .ToListAsync(cancellationToken);

var permissionsToRemove = permissions
    .Where(c => c.ClaimValue is not null && !requestedPermissions.Contains(c.ClaimValue))
    .ToList();

foreach (ApplicationRoleClaim permission in permissionsToRemove)
{
    permission.IsDeleted = true;
}
```

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
5. If editing permission-sync code, match the existing file's pattern and response shape.

## 5. Common Mistakes
- Allowing permission edits for the Admin role.
- Mixing route parameter names (`roleId`, `id`) incorrectly between route templates and handler parameters.
- Hard-deleting permission claims instead of matching the existing slice behavior.
- Introducing a new permission-sync style instead of extending the current slice.

## 6. Evolution Triggers
- Role permission storage changes.
- Admin/basic role protection rules change.
- Role read/write flows split into additional slices.

## References (real code)
- `src/AditiKraft.Krafter.Backend/Features/Roles/CreateRole.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/GetRoles.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/GetRoleById.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/GetRoleByIdWithPermissions.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/UpdateRole.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/UpdateRolePermissions.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/DeleteRole.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/Common/RoleService.cs`

---
Last Updated: 2026-04-28
Verified Against: src/AditiKraft.Krafter.Backend/Features/Roles/CreateRole.cs, src/AditiKraft.Krafter.Backend/Features/Roles/GetRoles.cs, src/AditiKraft.Krafter.Backend/Features/Roles/GetRoleById.cs, src/AditiKraft.Krafter.Backend/Features/Roles/GetRoleByIdWithPermissions.cs, src/AditiKraft.Krafter.Backend/Features/Roles/UpdateRole.cs, src/AditiKraft.Krafter.Backend/Features/Roles/UpdateRolePermissions.cs, src/AditiKraft.Krafter.Backend/Features/Roles/DeleteRole.cs, src/AditiKraft.Krafter.Backend/Features/Roles/Common/RoleService.cs, src/AditiKraft.Krafter.Contracts/Common/Auth/AppClaimTypes.cs, src/AditiKraft.Krafter.Contracts/Contracts/Roles/RoleConstants.cs, src/AditiKraft.Krafter.Contracts/Common/ApiRoutes.cs
---
