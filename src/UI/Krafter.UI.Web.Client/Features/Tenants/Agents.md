# Tenants UI Feature AI Instructions

> **SCOPE**: Tenant list and create/update dialog.
> **PARENT**: See also: ../../../Agents.md

## 1. Core Principles
- Use `ApiCallService` for all tenant API calls.
- Tenant list uses `Close(object? result)`; keep this signature if editing the page.
- Use `CreateOrUpdateTenant` dialog with `TenantInput`.

## 2. Decision Tree
- List page? Use `Features/Tenants/Tenants.razor` + `.razor.cs`.
- Create/update tenant? Use `CreateOrUpdateTenant` dialog.
- Delete tenant? Use `DialogService.Confirm()` + `DeleteTenantAsync`.

## 3. Code Templates

### Create/Update Dialog Submit
```csharp
Response result = await api.CallAsync(
    () => tenantsApi.CreateOrUpdateTenantAsync(input),
    successMessage: "Tenant saved successfully");
```

## 4. Checklist
1. Use `KrafterRoute.Tenants` as `RoutePath`.
2. Use `ApiCallService` for list and delete.
3. Keep `Close(object? result)` on the Tenants list page.

## 5. Common Mistakes
- Skipping confirmation before delete.
- Changing the `Close` signature in the Tenants list page.

## 6. Evolution Triggers
- Tenant creation flow changes.
- Tenant list filtering or history behavior changes.

## References (real code)
- `src/UI/Krafter.UI.Web.Client/Features/Tenants/Tenants.razor.cs`
- `src/UI/Krafter.UI.Web.Client/Features/Tenants/CreateOrUpdateTenant.razor.cs`

---
Last Updated: 2026-01-25
Verified Against: Features/Tenants/Tenants.razor.cs, Features/Tenants/CreateOrUpdateTenant.razor.cs
---
