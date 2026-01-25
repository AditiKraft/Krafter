# UI AI Instructions (Blazor + Radzen)

> **SCOPE**: Blazor pages/components, dialogs, and API integration via Refit.
> **PARENT**: See also: ../Agents.md

## Quick Start: New UI Feature
1. Ensure Shared DTOs + routes exist in `src/Krafter.Shared/`.
2. Add a Refit interface in `src/UI/Krafter.UI.Web.Client/Infrastructure/Refit/` (see `src/UI/Krafter.UI.Web.Client/Infrastructure/Refit/Agents.md`).
3. Register the Refit client in `Infrastructure/Refit/RefitServiceExtensions.cs`.
4. Create `Features/<Feature>/<Feature>s.razor` and add `.razor.cs` if you need logic.
5. Create `Features/<Feature>/CreateOrUpdate<Feature>.razor` and `.razor.cs` for form logic.
6. Wrap ALL API calls with `ApiCallService.CallAsync(...)`.
7. Add menu item in `Infrastructure/Services/MenuService.cs`.
8. Update `_Imports.razor` with the new contract namespace.

## Core Rules
- Use DTOs from `Krafter.Shared.Contracts.*`.
- Use `ApiCallService` for every Refit call.
- Use `KrafterRoute` from Shared for `RoutePath`.
- Add `@attribute [MustHavePermission(...)]` to list pages.
- List pages implement `IDisposable` and unsubscribe `dialogService.OnClose`.
- Delete flow uses `DialogService.Confirm()` + Refit delete endpoint.

## File Placement
- List page: `Features/<Feature>/<Feature>s.razor` (+ `.razor.cs` if needed)
- Form dialog: `Features/<Feature>/CreateOrUpdate<Feature>.razor` (+ `.razor.cs` if needed)
- Feature-shared UI pieces: `Features/<Feature>/_Shared/`
- Refit interfaces: `Infrastructure/Refit/`
- Menu: `Infrastructure/Services/MenuService.cs`

## Minimal List Page Pattern
```csharp
public partial class Users(
    DialogService dialogService,
    ApiCallService api,
    IUsersApi usersApi) : ComponentBase, IDisposable
{
    public const string RoutePath = KrafterRoute.Users;
    private GetRequestInput requestInput = new();
    private Response<PaginationResponse<UserDto>>? response = new() { Data = new PaginationResponse<UserDto>() };

    protected override async Task OnInitializedAsync()
    {
        LocalAppSate.CurrentPageTitle = "Users";
        dialogService.OnClose += Close;
        await GetListAsync();
    }

    private async Task GetListAsync()
    {
        response = await api.CallAsync(() => usersApi.GetUsersAsync(requestInput));
    }

    private async Task Delete(UserDto user)
    {
        bool? confirmed = await dialogService.Confirm(
            $"Are you sure you want to delete user '{user.FirstName} {user.LastName}'?",
            "Delete User",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirmed == true)
        {
            Response result = await api.CallAsync(
                () => usersApi.DeleteUserAsync(user.Id),
                successMessage: "User deleted successfully");

            if (!result.IsError)
                await GetListAsync();
        }
    }

    private async void Close(object? result)
    {
        if (result is not bool)
            return;
        await GetListAsync();
    }

    public void Dispose()
    {
        dialogService.OnClose -= Close;
        dialogService.Dispose();
    }
}
```

## Related Agents
- `src/UI/Krafter.UI.Web.Client/Infrastructure/Refit/Agents.md`

## References (real code)
- `src/UI/Krafter.UI.Web.Client/Features/Users/Users.razor.cs`
- `src/UI/Krafter.UI.Web.Client/Features/Roles/Roles.razor.cs`
- `src/UI/Krafter.UI.Web.Client/Features/Tenants/Tenants.razor.cs`
- `src/UI/Krafter.UI.Web.Client/Infrastructure/Refit/IUsersApi.cs`
- `src/UI/Krafter.UI.Web.Client/Infrastructure/Refit/IRolesApi.cs`
- `src/UI/Krafter.UI.Web.Client/Infrastructure/Refit/ITenantsApi.cs`

## Common Mistakes
- Calling Refit directly without `ApiCallService`.
- Forgetting to unsubscribe `dialogService.OnClose` on `Dispose`.
- Using UI-local route/permission constants instead of Shared.
- Refit route parameter name mismatch (e.g., `{id}` requires `id`).
- Skipping delete confirmation.

## Evolution & Maintenance

- Update this file when ApiCallService or UI lifecycle patterns change.

---
Last Updated: 2026-01-25
Verified Against: Features/Users/Users.razor.cs, Features/Roles/Roles.razor.cs, Features/Tenants/Tenants.razor.cs, Infrastructure/Refit/IUsersApi.cs, Infrastructure/Refit/IRolesApi.cs, Infrastructure/Refit/ITenantsApi.cs, Infrastructure/Refit/IAuthApi.cs, _Imports.razor
---
