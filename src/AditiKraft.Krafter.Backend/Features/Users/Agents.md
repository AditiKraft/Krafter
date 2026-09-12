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

### Shared User Changes
`CreateUser` and `UpdateUser` delegate to `IUserMutationService`. `UserMutationService` owns user creation, profile updates, role synchronization, and account email jobs. `IUserService` is for permission queries.

For tenant admin email changes, call `UpdateEmailAsync(id, email, cancellationToken)`. This preserves the user's profile and roles. Check the returned response before saving the tenant email.

Role synchronization checks that requested roles belong to the current tenant. New users receive the Basic role. Existing profile-only updates preserve roles when the list is omitted or empty.

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
- `src/AditiKraft.Krafter.Backend/Features/Users/Common/UserMutationService.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/CreateUser.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/UpdateUser.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/DeleteUser.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/ChangePassword.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/ForgotPassword.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/ResetPassword.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/GetUserRoles.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/GetUserPermissions.cs`

---
Last Updated: 2026-09-09
Verified Against: src/AditiKraft.Krafter.Backend/Features/Users/Common/UserMutationService.cs, src/AditiKraft.Krafter.Backend/Features/Users/CreateUser.cs, src/AditiKraft.Krafter.Backend/Features/Users/UpdateUser.cs, src/AditiKraft.Krafter.Backend/Features/Users/DeleteUser.cs, src/AditiKraft.Krafter.Backend/Features/Users/ChangePassword.cs, src/AditiKraft.Krafter.Backend/Features/Users/ForgotPassword.cs, src/AditiKraft.Krafter.Backend/Features/Users/ResetPassword.cs, src/AditiKraft.Krafter.Backend/Features/Users/GetUserRoles.cs, src/AditiKraft.Krafter.Backend/Features/Users/GetUserPermissions.cs, src/AditiKraft.Krafter.Backend/Features/Users/Common/UserService.cs, src/AditiKraft.Krafter.Contracts/Contracts/Roles/RoleConstants.cs
---
