# Roles UI Feature AI Instructions

> **SCOPE**: Role list, create/update dialog, and permissions UI.
> **PARENT**: See also: ../../../AGENTS.md

## 1. Core Principles
- Use the request validator in Contracts. Do not add a duplicate UI validator.
- Use `ApiCallService` for all role API calls.
- Await each `DialogService.OpenAsync` result. Reload only when the result is `true`.
- Build grouped permissions from `PermissionCatalog.All` in the dialog.
- Fetch/display permissions only with `RoleClaims.View`; allow selection changes only with `RoleClaims.Update`. Use UI display policies for those checks. Metadata-only editors must remain usable without claim privileges and submit `Permissions = null`.
- Editors that can change permissions cannot save existing roles until permissions load successfully. Keep Save and the permission selector disabled while loading or after a failed load. Show a retry action on failure. View-only editors may save metadata after a failed read, always omitting permissions.
- An empty permission list from a successful load is valid. A failed response, missing role, or null permission list is not an empty selection.
- Compare selections as sets against the loaded baseline. Send null for unchanged/omitted permissions, a nonempty list for an authorized changed set, and `[]` for an intentional clear. The same nullable request contract supports metadata-only creation.
- Use a Task-returning submit handler and check `CanSave` inside it as well as on the Save button.
- Use `EditorDialogBase` for pending-save dismissal and disposal guards; copy the outgoing request before awaiting it.

## 2. Decision Tree
- List page? Use `Features/Roles/Roles.razor` + `.razor.cs`.
- Create/update role? Use `CreateOrUpdateRole` dialog with `UserDetails` parameter.
- Need permissions UI? Use grouped permissions + `GetRolePermissionsAsync`.

## 3. Code Templates

### Group Permissions
```csharp
GroupedData = PermissionCatalog.All.GroupBy(c => c.Resource)
    .SelectMany(i => new GroupPermissionData[] { new() { Resource = i.Key } }
        .Concat(i.Select(o => new GroupPermissionData
        {
            Description = o.Description,
            Action = o.Action,
            IsBasic = o.IsBasic,
            IsRoot = o.IsRoot,
            FinalPermission = PermissionDefinition.NameFor(o.Action, o.Resource)
        }))).ToList();
```

### Prefill Role Permissions
```csharp
Response<RoleDto> response = await api.CallAsync(
    () => rolesApi.GetRolePermissionsAsync(UserDetails.Id),
    showErrorNotification: true);
if (response is not { IsError: false, Data.Permissions: not null })
{
    permissionLoadFailed = true;
    return;
}

CreateUserRequest.Permissions = [.. response.Data.Permissions];
originalPermissions = new HashSet<string>(response.Data.Permissions, StringComparer.Ordinal);
permissionsLoaded = true;
```

## 4. Checklist
1. Use `ApiRoutes.Roles` as `RoutePath`.
2. Use `ApiCallService` for list and delete.
3. Await the dialog result and reload only after a successful save.
4. Use grouped permissions + `GetRolePermissionsAsync` in dialog.

## 5. Common Mistakes
- Skipping grouped permissions setup.
- Converting a permission load failure into an empty list and allowing Save. This can clear existing permissions.
- Subscribing to the shared `OnClose` event or disposing the injected dialog service.

## 6. Evolution Triggers
- Permission model changes.
- Role permission UI changes.

## References (real code)
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Roles/Roles.razor.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Roles/CreateOrUpdateRole.razor.cs`

---
Last Updated: 2026-11-08
Verified Against: src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Roles/Roles.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Roles/CreateOrUpdateRole.razor.cs, src/AditiKraft.Krafter.Contracts/Common/Auth/Permissions/PermissionCatalog.cs, src/AditiKraft.Krafter.Contracts/Common/ApiRoutes.cs
---
