# Users UI Feature AI Instructions

> **SCOPE**: User list, create/update dialog, and password flows.
> **PARENT**: See also: ../../../AGENTS.md

## 1. Core Principles
- Use `ApiCallService` for all user API calls.
- After successful password change/reset, check `IAuthenticationService.LogoutAsync` to confirm cookies and browser auth state were cleared. If it fails, show the retryable error and offer sign-out again without resubmitting the password mutation. The backend revokes old sessions; navigation alone leaves stale client state.
- Await each `DialogService.OpenAsync` result. Reload only when the result is `true`.
- Fetch/display memberships only with `UserRoles.View`, and allow changes only with `UserRoles.Update` after a successful nonnull `GetUserRolesAsync` response. Disable membership editing and Save for membership editors during pending/failed loads, and show a retry action. Keep profile edits intact across retry.
- Profile-only editors skip unavailable membership reads and send an empty `Roles` list, which intentionally preserves backend memberships. Unchanged memberships also use an empty payload. Do not change the backend's empty-list semantics; explain that this editor cannot remove every role rather than silently claiming that an empty replacement was saved.
- Use the shared `EditorDialogBase` lifecycle and busy guard. Submit returns `Task`, copies the request before awaiting, and must not close a newer dialog after this component is disposed.

## 2. Decision Tree
- List page? Use `Features/Users/Users.razor` + `.razor.cs`.
- Create/update user? Use `CreateOrUpdateUser` dialog with `UserInput` parameter.
- Change password? Use `ChangePassword` page.
- Forgot/reset password? Use `ForgotPassword` and `ResetPassword` pages.

## 3. Code Templates

### Load User Roles in Dialog
```csharp
Response<List<UserRoleDto>> response = await api.CallAsync(
    () => usersApi.GetUserRolesAsync(UserInput.Id), showErrorNotification: true);
if (IsDisposed) return;
if (response is not { IsError: false, Data: not null })
{
    roleLoadFailed = true;
    return;
}

CreateUserRequest.Roles = response.Data.Where(role => role.Enabled && !string.IsNullOrEmpty(role.RoleId))
    .Select(role => role.RoleId!).Distinct(StringComparer.Ordinal).ToList();
originalRoles = new HashSet<string>(CreateUserRequest.Roles, StringComparer.Ordinal);
rolesLoaded = true;
```

### Delete User (Confirm + API)
```csharp
bool? confirmed = await dialogService.Confirm(
    $"Are you sure you want to delete user '{user.FirstName} {user.LastName}'?",
    "Delete User",
    new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

if (confirmed == true)
{
    Response result = await api.CallAsync(
        () => usersApi.DeleteUserAsync(user.Id),
        successMessage: "User deleted successfully");
}
```

## 4. Checklist
1. Use `ApiRoutes.Users` as `RoutePath`.
2. Use `ApiCallService` for list and delete.
3. Use `CreateOrUpdateUser` dialog for create/edit.
4. Await the dialog result and reload only after a successful save.

## 5. Common Mistakes
- Skipping role prefill when editing an existing user.
- Converting a failed role lookup into an editable empty selection, or sending an incomplete nonempty replacement.
- Subscribing to the shared `OnClose` event or disposing the injected dialog service.

## 6. Evolution Triggers
- User role/permission UI changes.
- Password flow UI changes.

## References (real code)
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/Users.razor.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/CreateOrUpdateUser.razor.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/ChangePassword.razor.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/ForgotPassword.razor.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/ResetPassword.razor.cs`

---
Last Updated: 2026-11-08
Verified Against: src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/Users.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/CreateOrUpdateUser.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/ChangePassword.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/ForgotPassword.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/ResetPassword.razor.cs, src/AditiKraft.Krafter.Contracts/Common/ApiRoutes.cs
---
