# Users Feature AI Instructions

> **SCOPE**: User CRUD, roles, permissions, and password flows.
> **PARENT**: See also: ../../Agents.md

## 1. Core Principles
- Use `UserManager<ApplicationUser>` and `RoleManager<ApplicationRole>` for all user operations.
- On create, ensure the Basic role is assigned (`RoleConstants.Basic`).
- Send account and password emails via `IJobService.EnqueueAsync(...)`.
- Soft-delete users and related roles; never hard-delete.

## 2. Decision Tree
- Creating a user? Follow `CreateUser` pattern.
- Updating a user? Follow `UpdateUser` pattern.
- Deleting a user? Follow `DeleteUser` soft-delete pattern.
- Password changes or reset flows? Use `ChangePassword`, `ForgotPassword`, `ResetPassword` patterns.
- Need user roles or permissions? Use `GetUserRoles` and `GetUserPermissions` patterns.

## 3. Code Templates

### Role Sync (CreateUser / UpdateUser)
```csharp
List<ApplicationUserRole> existingRoles = await db.UserRoles
    .IgnoreQueryFilters()
    .Where(c => c.TenantId == tenantGetterService.Tenant.Id && c.UserId == user.Id)
    .ToListAsync();

var rolesToRemove = existingRoles.Where(r => !request.Roles.Contains(r.RoleId)).ToList();
var rolesToUpdate = existingRoles.Where(r => request.Roles.Contains(r.RoleId)).ToList();
var rolesToAdd = request.Roles
    .Where(roleId => !existingRoles.Any(er => er.RoleId == roleId))
    .Select(roleId => new ApplicationUserRole { RoleId = roleId, UserId = user.Id })
    .ToList();

foreach (ApplicationUserRole role in rolesToRemove)
{
    role.IsDeleted = true;
}
```

## 4. Checklist
1. Use `UserManager`/`RoleManager` for user and role operations.
2. Ensure Basic role is added when creating a new user.
3. If `UpdateTenantEmail` is true, update tenant admin email in `TenantDbContext`.
4. Enqueue email jobs for account creation and password flows.
5. Soft-delete user roles when removing a user.

## 5. Common Mistakes
- Skipping Basic role assignment for new users.
- Updating user email without handling tenant admin email when requested.
- Hard-deleting users or roles instead of soft delete.

## 6. Evolution Triggers
- Role assignment rules change.
- Password or email notification flows change.
- User role/permission query patterns change.

## References (real code)
- `src/AditiKraft.Krafter.Backend/Features/Users/CreateUser.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/UpdateUser.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/DeleteUser.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/ChangePassword.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/ForgotPassword.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/ResetPassword.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/GetUserRoles.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/GetUserPermissions.cs`

---
Last Updated: 2026-04-28
Verified Against: src/AditiKraft.Krafter.Backend/Features/Users/CreateUser.cs, src/AditiKraft.Krafter.Backend/Features/Users/UpdateUser.cs, src/AditiKraft.Krafter.Backend/Features/Users/DeleteUser.cs, src/AditiKraft.Krafter.Backend/Features/Users/ChangePassword.cs, src/AditiKraft.Krafter.Backend/Features/Users/ForgotPassword.cs, src/AditiKraft.Krafter.Backend/Features/Users/ResetPassword.cs, src/AditiKraft.Krafter.Backend/Features/Users/GetUserRoles.cs, src/AditiKraft.Krafter.Backend/Features/Users/GetUserPermissions.cs, src/AditiKraft.Krafter.Backend/Features/Users/Common/UserService.cs, src/AditiKraft.Krafter.Contracts/Contracts/Roles/RoleConstants.cs
---
