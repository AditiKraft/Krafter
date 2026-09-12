# UI AI Instructions (Blazor + Radzen)

> **SCOPE**: Blazor pages/components, dialogs, and API integration via Refit.
> **PARENT**: See also: ../../Agents.md

## Quick Start: New UI Feature
1. If `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/<Feature>/Agents.md` exists, read it first.
2. Ensure Shared DTOs + routes exist in `src/AditiKraft.Krafter.Contracts/`.
3. Add `I<Feature>Api.cs` beside the pages in `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/<Feature>/`. Read `src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Refit/Agents.md` for route and registration rules.
4. Register the Refit client in `Infrastructure/Refit/RefitServiceExtensions.cs`.
5. Create `Features/<Feature>/<Feature>s.razor` and add `.razor.cs` if you need logic.
6. Create `Features/<Feature>/CreateOrUpdate<Feature>.razor` and `.razor.cs` for form logic.
7. Wrap feature Refit calls with `ApiCallService.CallAsync(...)`; auth flows should go through `IAuthenticationService`.
8. Add menu item in `Infrastructure/Services/MenuService.cs`.
9. Update `_Imports.razor` with the new contract namespace.

For URL settings, server API connections, tenant routing, or Google callbacks, read [Configure application URLs](../../docs/url-configuration.md). Browser startup loads public `AppUrls` from `/configuration/urls`; server URL resolution belongs in UI.Web `Infrastructure/Hosting/UiUrlConfiguration.cs`.

## Core Rules
- Use DTOs from `AditiKraft.Krafter.Contracts.Contracts.*`.
- Use `ApiCallService` for Refit calls made directly from UI components; auth flows go through `IAuthenticationService`.
- Use `ApiRoutes` from Shared for `RoutePath`.
- Add `@attribute [MustHavePermission(...)]` to list pages.
- Await each `DialogService.OpenAsync` result and reload the list only when it is `true`.
- The DI scope owns `DialogService`. Pages must not dispose it or subscribe to its shared `OnClose` event.
- Refit converts JSON timestamps to UTC on send and to local time on browser reads. Bind UI fields directly to these local values. See [Refit instructions](AditiKraft.Krafter.UI.Web.Client/Infrastructure/Refit/Agents.md) for server rendering and date-only rules.
- Delete flow uses `DialogService.Confirm()` + Refit delete endpoint.

## File Placement
- Shared colors and component styles: `AditiKraft.Krafter.UI.Web.Client/wwwroot/app.css`. Use its `--kr-*` semantic tokens and Radzen mappings. Brand Blue is the primary action color; surfaces are solid and neutral.
- Load `app.css` after `RadzenTheme` in `UI.Web/Components/App.razor`. Dark tokens follow `#radzen-theme-link` because Radzen changes the stylesheet URL, not a class on the document. Keep Auto, Light, and Dark preferences working through `ThemeManager`.
- Map grid frozen-cell and loading colors explicitly to surface tokens. Frozen headers use the header surface; the loading mask stays translucent so existing rows remain visible during refresh.
- List page: `Features/<Feature>/<Feature>s.razor` (+ `.razor.cs` if needed)
- Form dialog: `Features/<Feature>/CreateOrUpdate<Feature>.razor` (+ `.razor.cs` if needed)
- Feature-shared UI pieces: `Features/<Feature>/Common/`
- Feature API interface: `Features/<Feature>/I<Feature>Api.cs`
- Shared HTTP client registration and tenant handler: `Infrastructure/Refit/`
- Authentication services, state provider, token storage, and auth handler: `Infrastructure/Auth/`
- Common server UI registration: `AditiKraft.Krafter.UI.Web/Infrastructure/Hosting/UiHostServiceRegistration.cs`. Both hosting variants call `AddUiHostServices(configuration, hostingMode)` (split host is the default); keep their authentication setup and endpoint mapping in their own `Program.cs`.
- Server authentication implementations: `AditiKraft.Krafter.UI.Web/Infrastructure/Auth/`
- Shared visual components: `Common/Components/`
- Shared UI state and models: `Common/Models/`
- Menu: `Infrastructure/Services/MenuService.cs`

Keep each feature's pages, dialogs, and API interface together. Keep namespaces aligned with folders. Use explicit feature imports for cross-feature API calls. Put a shared implementation in `Infrastructure` only when more than one feature uses it.

## Minimal List Page Pattern
```csharp
public partial class Users(
    DialogService dialogService,
    ApiCallService api,
    IUsersApi usersApi) : ComponentBase
{
    public const string RoutePath = ApiRoutes.Users;
    private GetRequestInput requestInput = new();
    private Response<PaginationResponse<UserDto>>? response = new() { Data = new PaginationResponse<UserDto>() };

    protected override async Task OnInitializedAsync()
    {
        LocalAppState.CurrentPageTitle = "Users";
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

    private async Task AddUser()
    {
        object? result = await dialogService.OpenAsync<CreateOrUpdateUser>("Add New User",
            new Dictionary<string, object> { { "UserInput", new UserDto() } },
            new DialogOptions { Width = "40vw", Resizable = true, Draggable = true, Top = "5vh" });
        if (result is true)
        {
            await GetListAsync();
        }
    }
}
```

## Related Agents
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Refit/Agents.md`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/Agents.md`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/Agents.md`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Roles/Agents.md`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Tenants/Agents.md`

## References (real code)
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/Users.razor.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Roles/Roles.razor.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Tenants/Tenants.razor.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/IUsersApi.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Roles/IRolesApi.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Tenants/ITenantsApi.cs`

## Common Mistakes
- Calling Refit directly without `ApiCallService`.
- Reloading on canceled dialogs or handling another page's dialog through the shared `OnClose` event.
- Disposing an injected service that the DI scope owns.
- Using UI-local route/permission constants instead of Shared.
- Refit route parameter name mismatch (e.g., `{id}` requires `id`).
- Skipping delete confirmation.

## Evolution & Maintenance

- Update this file when ApiCallService or UI lifecycle patterns change.

---
Last Updated: 2026-09-12
Verified Against: src/UI/AditiKraft.Krafter.UI.Web.Client/wwwroot/app.css, src/UI/AditiKraft.Krafter.UI.Web/Components/App.razor, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/Login.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/GoogleCallback.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/Users.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Roles/Roles.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Tenants/Tenants.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/IUsersApi.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Roles/IRolesApi.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Tenants/ITenantsApi.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/IAuthApi.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/_Imports.razor, src/AditiKraft.Krafter.Contracts/Common/ApiRoutes.cs, docs/url-configuration.md, src/AditiKraft.Krafter.Contracts/Common/AppUrls.cs, src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Hosting/UiUrlConfiguration.cs
---



