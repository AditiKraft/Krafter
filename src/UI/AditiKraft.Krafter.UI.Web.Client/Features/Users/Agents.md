# Users UI Feature AI Instructions

> **SCOPE**: User list, create/update dialog, and password flows.
> **PARENT**: See also: ../../../Agents.md

## 1. Core Principles
- Use `ApiCallService` for all user API calls.
- Await each `DialogService.OpenAsync` result. Reload only when the result is `true`.
- Populate role selections by calling `GetUserRolesAsync` when editing a user.

## 2. Decision Tree
- List page? Use `Features/Users/Users.razor` + `.razor.cs`.
- Create/update user? Use `CreateOrUpdateUser` dialog with `UserInput` parameter.
- Change password? Use `ChangePassword` page.
- Forgot/reset password? Use `ForgotPassword` and `ResetPassword` pages.

## 3. Code Templates

### Load User Roles in Dialog
```csharp
UserRoles = await api.CallAsync(() => usersApi.GetUserRolesAsync(UserInput.Id), showErrorNotification: true);
CreateUserRequest.Roles = UserRoles?.Data?
    .Where(c => !string.IsNullOrEmpty(c.RoleId))
    .Select(c => c.RoleId!)
    .ToList() ?? new List<string>();
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
Last Updated: 2026-09-09
Verified Against: src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/Users.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/CreateOrUpdateUser.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/ChangePassword.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/ForgotPassword.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/ResetPassword.razor.cs, src/AditiKraft.Krafter.Contracts/Common/ApiRoutes.cs
---
