# Tenants UI Feature AI Instructions

> **SCOPE**: Tenant list and create/update dialog.
> **PARENT**: See also: ../../../Agents.md

## 1. Core Principles
- Use the request validator in Contracts. Do not add a duplicate UI validator.
- Use `ApiCallService` for all tenant API calls.
- Await each `DialogService.OpenAsync` result. Reload only when the result is `true`.
- Use `CreateOrUpdateTenant` dialog with `TenantInput`.

## 2. Decision Tree
- List page? Use `Features/Tenants/Tenants.razor` + `.razor.cs`.
- Create/update tenant? Use `CreateOrUpdateTenant` dialog.
- Delete tenant? Use `DialogService.Confirm()` + `DeleteTenantAsync`.

## 3. Code Templates

### Create/Update Dialog Submit
```csharp
Response result = await api.CallAsync(
    () => tenantsApi.CreateTenantAsync(input),
    successMessage: "Tenant created successfully");
```

## 4. Checklist
1. Use `ApiRoutes.Tenants` as `RoutePath`.
2. Use `ApiCallService` for list and delete.
3. Await the dialog result and reload only after a successful save.

## 5. Common Mistakes
- Skipping confirmation before delete.
- Subscribing to the shared `OnClose` event or disposing the injected dialog service.

## 6. Evolution Triggers
- Tenant creation flow changes.
- Tenant list filtering or history behavior changes.

## References (real code)
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Tenants/Tenants.razor.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Tenants/CreateOrUpdateTenant.razor.cs`

---
Last Updated: 2026-09-09
Verified Against: src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Tenants/Tenants.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Tenants/CreateOrUpdateTenant.razor.cs, src/AditiKraft.Krafter.Contracts/Common/ApiRoutes.cs
---
