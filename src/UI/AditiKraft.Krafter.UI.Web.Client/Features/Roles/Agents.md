# Roles UI Feature AI Instructions

> **SCOPE**: Role list, create/update dialog, and permissions UI.
> **PARENT**: See also: ../../../Agents.md

## 1. Core Principles
- Use `ApiCallService` for all role API calls.
- Role list uses `Close(object? result)`; keep this signature if editing the page.
- Build grouped permissions from `KrafterPermissions.All` in the dialog.

## 2. Decision Tree
- List page? Use `Features/Roles/Roles.razor` + `.razor.cs`.
- Create/update role? Use `CreateOrUpdateRole` dialog with `UserDetails` parameter.
- Need permissions UI? Use grouped permissions + `GetRolePermissionsAsync`.

## 3. Code Templates

### Group Permissions
```csharp
GroupedData = KrafterPermissions.All.GroupBy(c => c.Resource)
    .SelectMany(i => new GroupPermissionData[] { new() { Resource = i.Key } }
        .Concat(i.Select(o => new GroupPermissionData
        {
            Description = o.Description,
            Action = o.Action,
            IsBasic = o.IsBasic,
            IsRoot = o.IsRoot,
            FinalPermission = KrafterPermission.NameFor(o.Action, o.Resource)
        }))).ToList();
```

### Prefill Role Permissions
```csharp
Response<RoleDto> rolePermissions = await api.CallAsync(
    () => rolesApi.GetRolePermissionsAsync(UserDetails.Id),
    showErrorNotification: true);
CreateUserRequest.Permissions = rolePermissions?.Data?.Permissions ?? new List<string>();
```

## 4. Checklist
1. Use `KrafterRoute.Roles` as `RoutePath`.
2. Use `ApiCallService` for list and delete.
3. Keep `Close(object? result)` on the Roles list page.
4. Use grouped permissions + `GetRolePermissionsAsync` in dialog.

## 5. Common Mistakes
- Skipping grouped permissions setup.
- Changing the `Close` signature in the Roles list page.

## 6. Evolution Triggers
- Permission model changes.
- Role permission UI changes.

## References (real code)
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Roles/Roles.razor.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Roles/CreateOrUpdateRole.razor.cs`

---
Last Updated: 2026-01-25
Verified Against: Features/Roles/Roles.razor.cs, Features/Roles/CreateOrUpdateRole.razor.cs
---
