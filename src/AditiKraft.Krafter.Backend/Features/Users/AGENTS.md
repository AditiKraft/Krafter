# Users Feature AI Instructions

> **SCOPE**: User CRUD, roles, permissions, and password flows.
> **PARENT**: See also: ../../AGENTS.md

## 1. Core Principles
- Use `UserManager<ApplicationUser>` and `RoleManager<ApplicationRole>` for all user operations.
- On create, ensure the Basic role is assigned (`RoleConstants.Basic`).
- Send account and password emails via `IJobService.EnqueueAsync(...)`.
- HTML-encode dynamic names, tenant labels, credentials, and existing link attribute values when composing email bodies. Keep template-owned markup intact; validators that accept display names do not make those names safe HTML.
- User creation and its mandatory Basic membership share `DomainTransaction`. Profile/role changes and requested tenant-admin email synchronization also commit together. Check cancellation before commit, finish commit independently once started, and clear tracked state on rollback. Enqueue account credentials only after successful commit with `CancellationToken.None`; job persistence is a separate durable boundary and enqueue failures propagate.
- Deleted usernames/emails remain reserved within their tenant, ignoring case. Include deleted history with an explicit `TenantId` predicate and translate known identity unique-index conflicts to 409. Validate raw/normalized identifier lengths and the configured Identity username alphabet before writes.
- Anonymous forgot/reset flows use the authentication throttle and cancellation. Forgot returns the same public success envelope for known, unknown and ineligible accounts, including queue failures; it logs only a fixed message and exception type for non-cancellation enqueue errors. Invalid resets share one public error. Valid reset retains password/version atomicity.
- Password change/reset must persist `AuthenticationVersion` with the password in one successful Identity update. Advance it before calling Identity; detach the failed user on failure. A second save can leave changed passwords with valid old sessions during concurrency conflicts.
- Soft-delete users and related roles; never hard-delete.
- Soft deletion must rotate `ConcurrencyStamp` and `SecurityStamp` and advance `AuthenticationVersion` in the same save. A stale Identity full-row update must fail instead of restoring a deleted account or its prior sessions.

## 2. Decision Tree
- Creating a user? Follow `CreateUser` pattern.
- Updating a user? Follow `UpdateUser` pattern.
- Deleting a user? Follow `DeleteUser` soft-delete pattern.
- Password changes or reset flows? Use `ChangePassword`, `ForgotPassword`, `ResetPassword` patterns.
- Need user roles or permissions? Use `GetUserRoles` and `GetUserPermissions` patterns.

## 3. Code Templates

### Shared User Changes
`CreateUser` and `UpdateUser` delegate to `IUserMutationService`. `UserMutationService` owns user creation, profile updates, role synchronization, and account email jobs. `IUserService` is for permission queries.

For tenant admin email changes, call `UpdateEmailAsync(id, email, cancellationToken)` inside the shared registry/identity transaction owned by `UpdateTenant`. It preserves the user's profile and roles; check its response before committing. Standalone callers receive an identity transaction. User-driven owner email changes synchronize the current tenant by owner identity, including when an older registry email is stale. If an owner requests that synchronization but the registry is missing or deleted, return 409 and roll back the identity/profile changes; nonowner optional synchronization keeps its existing behavior.

Role synchronization checks that requested roles belong to the current tenant. New users receive the Basic role. Additional roles on create and actual membership changes on update require the current actor's live `Permissions.UserRoles.Update` permission. Changing another user's login email also requires that permission, even when the role list is omitted or unchanged, because email recovery can transfer the account's existing permissions. Compare emails using Identity normalization; self email changes and case-equivalent changes remain allowed. Profile-only updates preserve roles when the list is omitted or empty; an unchanged submitted role set is not synchronized. `UpdateEmailAsync` remains the trusted profile-only path for tenant admin synchronization.

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
Last Updated: 2026-11-08
Verified Against: src/AditiKraft.Krafter.Backend/Features/Users/Common/UserMutationService.cs, src/AditiKraft.Krafter.Backend/Features/Users/CreateUser.cs, src/AditiKraft.Krafter.Backend/Features/Users/UpdateUser.cs, src/AditiKraft.Krafter.Backend/Features/Users/DeleteUser.cs, src/AditiKraft.Krafter.Backend/Features/Users/ChangePassword.cs, src/AditiKraft.Krafter.Backend/Features/Users/ForgotPassword.cs, src/AditiKraft.Krafter.Backend/Features/Users/ResetPassword.cs, src/AditiKraft.Krafter.Backend/Features/Users/GetUserRoles.cs, src/AditiKraft.Krafter.Backend/Features/Users/GetUserPermissions.cs, src/AditiKraft.Krafter.Backend/Features/Users/Common/UserService.cs, src/AditiKraft.Krafter.Contracts/Contracts/Roles/RoleConstants.cs
---
