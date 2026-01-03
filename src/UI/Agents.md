# UI AI Instructions (Blazor + Radzen)

> **SCOPE**: Blazor pages, components, dialogs, services, API integration via Refit.

## 1. Core Principles
- **Hybrid Blazor**: WebAssembly + Server rendering.
- **Code-Behind**: ALWAYS split `.razor` (markup) and `.razor.cs` (logic).
- **Feature-Based**: Group by feature, not by component type.
- **Refit Interfaces**: Use typed Refit interfaces (`IUsersApi`, `IRolesApi`, etc.) for ALL API calls. Never use raw `HttpClient`.
- **External APIs**: Also use Refit for external/third-party APIs. Create interface in `Infrastructure/Refit/` and register in `RefitServiceExtensions.cs` without auth handlers.
- **Shared Contracts**: Use DTOs from `Krafter.Shared.Contracts.*` - never duplicate models in UI.

## 2. Decision Tree: Where Does This Code Go?

```
┌─────────────────────────────────────────────────────────────┐
│ What are you adding?                                        │
├─────────────────────────────────────────────────────────────┤
│ List/Grid page?                                             │
│   → Features/<Feature>/<Feature>s.razor + .razor.cs         │
│                                                             │
│ Create/Edit form dialog?                                    │
│   → Features/<Feature>/CreateOrUpdate<Feature>.razor        │
│                                                             │
│ Feature-specific dropdown/selector?                         │
│   → Features/<Feature>/_Shared/<Component>.razor            │
│                                                             │
│ Feature-specific service/validator?                         │
│   → Features/<Feature>/_Shared/<Service>.cs                 │
│                                                             │
│ Reusable layout component (navbar, sidebar)?                │
│   → Common/Components/Layout/                               │
│                                                             │
│ Reusable form component (search, dialog)?                   │
│   → Common/Components/Forms/ or Dialogs/                    │
│                                                             │
│ Infrastructure service (auth, API)?                         │
│   → Infrastructure/Services/ or Infrastructure/Api/         │
│                                                             │
│ Permission definition?                                      │
│   → Common/Permissions/KrafterPermissions.cs                │
│                                                             │
│ Route constant?                                             │
│   → Common/Constants/KrafterRoute.cs                        │
└─────────────────────────────────────────────────────────────┘
```

## 3. Directory Structure
```
src/UI/Krafter.UI.Web.Client/
├── Features/
│   ├── Users/
│   │   ├── Users.razor              ← List page (markup)
│   │   ├── Users.razor.cs           ← List page (logic)
│   │   ├── CreateOrUpdateUser.razor ← Form dialog
│   │   ├── CreateOrUpdateUser.razor.cs
│   │   └── _Shared/
│   │       ├── SingleSelectUserDropDown.razor
│   │       └── SingleSelectUserDropDown.razor.cs
│   └── <YourFeature>/
│       ├── <Feature>s.razor
│       ├── <Feature>s.razor.cs
│       ├── CreateOrUpdate<Feature>.razor
│       └── CreateOrUpdate<Feature>.razor.cs
├── Infrastructure/
│   ├── Refit/                       ← Refit API interfaces
│   │   ├── IAuthApi.cs              ← Auth endpoints
│   │   ├── IUsersApi.cs             ← User endpoints
│   │   ├── IRolesApi.cs             ← Role endpoints
│   │   ├── ITenantsApi.cs           ← Tenant endpoints
│   │   ├── IAppInfoApi.cs           ← App info endpoints
│   │   ├── RefitAuthHandler.cs      ← JWT token handler
│   │   ├── RefitTenantHandler.cs    ← Tenant header handler
│   │   └── RefitServiceExtensions.cs ← DI registration
│   ├── Services/MenuService.cs
│   └── Api/ClientSideApiService.cs
├── Common/
│   ├── Components/Layout/
│   ├── Permissions/KrafterPermissions.cs
│   └── Constants/KrafterRoute.cs
└── _Imports.razor                   ← Global usings (includes Shared contracts)
```

## 4. Code Templates

### 4.1 List Page - Markup (.razor)
```razor
@attribute [Route(RoutePath)]
@attribute [MustHavePermission(KrafterAction.View, KrafterResource.Products)]

<AuthorizeView Policy="@(KrafterPermission.NameFor(KrafterAction.Create, KrafterResource.Products))">
    <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" Gap="10px" JustifyContent="JustifyContent.End" class="mt-2 mb-4">
        <RadzenButton Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Primary" Icon="add_circle_outline" Text="Product" Click="@Add"/>
    </RadzenStack>
</AuthorizeView>

<RadzenDataGrid Responsive="true" 
                ShowCellDataAsTooltip="LocalAppSate.ShowCellDataAsTooltip" 
                @ref="@grid" 
                Density="LocalAppSate.Density" 
                AllowColumnResize="LocalAppSate.AllowColumnResize" 
                IsLoading="IsLoading" 
                Count="@(response?.Data?.TotalCount ?? 0)" 
                LoadData="@LoadData" 
                AllowSorting="true" 
                Data="@response.Data.Items" 
                AllowFiltering="true" 
                AllowPaging="true" 
                PageSize="@requestInput.MaxResultCount" 
                PagerHorizontalAlign="HorizontalAlign.Center">
    <Columns>
        <RadzenDataGridColumn Property="Name" Title="Name"/>
        <RadzenDataGridColumn Property="Price" Title="Price" FormatString="{0:C}"/>
        <RadzenDataGridColumn Property="IsActive" Title="Active"/>
        
        <RadzenDataGridColumn Title="Actions" Filterable="false" Sortable="false" TextAlign="TextAlign.Right" Frozen="true" FrozenPosition="FrozenColumnPosition.Right">
            <Template Context="data">
                <RadzenSplitButton ButtonStyle="ButtonStyle.Light" Shade="Shade.Lighter" Variant="Variant.Flat" AlwaysOpenPopup="true" Click="@(args => ActionClicked(args, data))" Text="Actions" Icon="settings_applications">
                    <ChildContent>
                        <AuthorizeView Policy="@(KrafterPermission.NameFor(KrafterAction.Update, KrafterResource.Products))">
                            <RadzenSplitButtonItem Text="Edit" Value="@KrafterAction.Update" Icon="edit"/>
                        </AuthorizeView>
                        <AuthorizeView Policy="@(KrafterPermission.NameFor(KrafterAction.Delete, KrafterResource.Products))">
                            <RadzenSplitButtonItem Text="Delete" Value="@KrafterAction.Delete" Icon="delete"/>
                        </AuthorizeView>
                    </ChildContent>
                </RadzenSplitButton>
            </Template>
        </RadzenDataGridColumn>
    </Columns>
</RadzenDataGrid>
```

### 4.2 List Page - Code-Behind (.razor.cs)
```csharp
using Krafter.Shared.Common.Enums;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Products;
using Krafter.UI.Web.Client.Common.Constants;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Common.Permissions;
using Krafter.UI.Web.Client.Infrastructure.Refit;
using Krafter.UI.Web.Client.Infrastructure.Services;

namespace Krafter.UI.Web.Client.Features.Products;

public partial class Products(
    CommonService commonService,
    DialogService dialogService,
    ApiCallService api,        // CRITICAL: Always inject ApiCallService
    IProductsApi productsApi
) : ComponentBase, IDisposable
{
    public const string RoutePath = KrafterRoute.Products;
    private RadzenDataGrid<ProductDto> grid = default!;
    private bool IsLoading = true;
    private GetRequestInput requestInput = new();
    private Response<PaginationResponse<ProductDto>>? response = new() 
    { 
        Data = new PaginationResponse<ProductDto>() 
    };

    protected override async Task OnInitializedAsync()
    {
        LocalAppSate.CurrentPageTitle = "Products";  // Set page title
        dialogService.OnClose += Close;              // Subscribe to dialog close
        await Get();
    }

    private async Task Get(bool resetPaginationData = false)
    {
        IsLoading = true;
        if (resetPaginationData)
            requestInput.SkipCount = 0;

        // CRITICAL: Use ApiCallService wrapper for ALL API calls
        // Pass GetRequestInput directly - Refit serializes all properties as query params
        response = await api.CallAsync(() => productsApi.GetProductsAsync(requestInput));
        
        IsLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadData(LoadDataArgs args)
    {
        IsLoading = true;
        await Task.Yield();
        requestInput.SkipCount = args.Skip ?? 0;
        requestInput.MaxResultCount = args.Top ?? 10;
        requestInput.Filter = args.Filter;
        requestInput.OrderBy = args.OrderBy;
        await Get();
    }

    private async Task Add()
    {
        await dialogService.OpenAsync<CreateOrUpdateProduct>("Add New Product",
            new Dictionary<string, object> { { "ProductInput", new ProductDto() } },
            new DialogOptions { Width = "40vw", Resizable = true, Draggable = true, Top = "5vh" });
    }

    private async Task Update(ProductDto product)
    {
        await dialogService.OpenAsync<CreateOrUpdateProduct>($"Update Product {product.Name}",
            new Dictionary<string, object> { { "ProductInput", product } },
            new DialogOptions { Width = "40vw", Resizable = true, Draggable = true, Top = "5vh" });
    }

    private async Task Delete(ProductDto input)
    {
        if (response?.Data?.Items?.Contains(input) == true)
        {
            await commonService.Delete(
                new DeleteRequestInput
                {
                    Id = input.Id,
                    DeleteReason = input.DeleteReason,
                    EntityKind = EntityKind.Product
                }, $"Delete Product {input.Name}");
        }
        else
        {
            grid.CancelEditRow(input);
            await grid.Reload();
        }
    }

    private async Task ActionClicked(RadzenSplitButtonItem? item, ProductDto data)
    {
        if (item is { Value: KrafterAction.Update })
            await Update(data);
        else if (item is { Value: KrafterAction.Create })
            await Add();
        else if (item is { Value: KrafterAction.Delete })
            await Delete(data);
    }

    // IMPORTANT: Use dynamic type for result parameter
    private async void Close(dynamic result)
    {
        if (result == null || !result.Equals(true))
            return;
        await Get();
    }

    public void Dispose()
    {
        dialogService.OnClose -= Close;  // CRITICAL: Unsubscribe to prevent memory leaks
        dialogService.Dispose();
    }
}
```

### 4.3 Form Dialog - Markup (.razor)
```razor
@using Krafter.Shared.Contracts.Products

<RadzenTemplateForm Data="@CreateRequest" Submit="@((CreateProductRequest __) => Submit(__))">
    <FluentValidationValidator Options="@(options => options.IncludeAllRuleSets())"/>
    <RadzenStack>
        <RadzenRow AlignItems="AlignItems.Center">
            <RadzenColumn Size="12" SizeMD="2">
                <RadzenLabel Text="Name" Component="Name"/>
            </RadzenColumn>
            <RadzenColumn Size="12" SizeMD="10">
                <RadzenTextBox Style="width: 100%" @bind-Value="@CreateRequest.Name" Name="Name"/>
            </RadzenColumn>
            <ValidationMessage style="font-size: 13px" For="@(() => CreateRequest.Name)"/>
        </RadzenRow>

        <RadzenRow AlignItems="AlignItems.Center">
            <RadzenColumn Size="12" SizeMD="2">
                <RadzenLabel Text="Price" Component="Price"/>
            </RadzenColumn>
            <RadzenColumn Size="12" SizeMD="10">
                <RadzenNumeric Style="width: 100%" @bind-Value="@CreateRequest.Price" Name="Price"/>
            </RadzenColumn>
            <ValidationMessage style="font-size: 13px" For="@(() => CreateRequest.Price)"/>
        </RadzenRow>

        <RadzenRow AlignItems="AlignItems.Center">
            <RadzenColumn Size="12" SizeMD="2">
                <RadzenLabel Text="Is Active" Component="IsActive"/>
            </RadzenColumn>
            <RadzenColumn Size="12" SizeMD="10">
                <RadzenCheckBox @bind-Value="@CreateRequest.IsActive" Name="IsActive"/>
            </RadzenColumn>
            <ValidationMessage style="font-size: 13px" For="@(() => CreateRequest.IsActive)"/>
        </RadzenRow>
    </RadzenStack>
    
    <RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.Center" Gap="1rem" Class="rz-mt-8 rz-mb-4">
        <RadzenButton ButtonType="ButtonType.Submit" IsBusy="isBusy" Size="ButtonSize.Medium" Icon="save" Text="Save"/>
        <RadzenButton ButtonStyle="ButtonStyle.Light" Variant="Variant.Flat" Size="ButtonSize.Medium" Icon="cancel" Text="Cancel" Click="@Cancel"/>
    </RadzenStack>
</RadzenTemplateForm>
```

### 4.4 Form Dialog - Code-Behind (.razor.cs)
```csharp
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Products;
using Krafter.UI.Web.Client.Infrastructure.Refit;
using Krafter.UI.Web.Client.Infrastructure.Services;
using Mapster;

namespace Krafter.UI.Web.Client.Features.Products;

public partial class CreateOrUpdateProduct(
    DialogService dialogService,
    ApiCallService api,        // CRITICAL: Always inject ApiCallService
    IProductsApi productsApi
) : ComponentBase
{
    [Parameter] public ProductDto? ProductInput { get; set; } = new();
    private CreateProductRequest CreateRequest = new();
    private CreateProductRequest OriginalCreateRequest = new();
    private bool isBusy = false;

    protected override async Task OnInitializedAsync()
    {
        if (ProductInput is not null)
        {
            CreateRequest = ProductInput.Adapt<CreateProductRequest>();
            OriginalCreateRequest = ProductInput.Adapt<CreateProductRequest>();
        }
    }

    private async void Submit(CreateProductRequest input)
    {
        if (ProductInput is not null)
        {
            isBusy = true;
            
            // CRITICAL: Use ApiCallService wrapper with success message
            Response result = await api.CallAsync(
                () => productsApi.CreateOrUpdateProductAsync(input),
                successMessage: "Product saved successfully");
            
            isBusy = false;
            StateHasChanged();
            
            if (result is { IsError: false })
                dialogService.Close(true);
        }
        else
        {
            dialogService.Close(false);
        }
    }

    private void Cancel() => dialogService.Close();
}
```

## 5. Permission Patterns

### 5.1 Page-Level Permission
```razor
@attribute [MustHavePermission(KrafterAction.View, KrafterResource.Products)]
```

### 5.2 Component-Level Permission
```razor
<AuthorizeView Policy="@KrafterPermission.NameFor(KrafterAction.Create, KrafterResource.Products)">
    <Authorized>
        <RadzenButton Text="Add Product" Click="@AddProduct"/>
    </Authorized>
</AuthorizeView>
```

## 6. API Calls via ApiCallService (CRITICAL)

> **⚠️ IMPORTANT**: ALL API calls in UI components MUST use `ApiCallService.CallAsync()` wrapper. NEVER call Refit interfaces directly!

### 6.1 ApiCallService Pattern (REQUIRED)

```csharp
// ✅ CORRECT - Always use ApiCallService wrapper
public partial class Products(
    ApiCallService api,        // REQUIRED - Always inject this
    IProductsApi productsApi,  // Refit interface
    DialogService dialogService
) : ComponentBase
{
    private async Task GetListAsync()
    {
        // ✅ CORRECT - Wrap Refit call with ApiCallService
        response = await api.CallAsync(() => productsApi.GetProductsAsync(
            requestInput.Id,
            requestInput.History,
            requestInput.IsDeleted,
            requestInput.Query,
            requestInput.Filter,
            requestInput.OrderBy,
            requestInput.SkipCount,
            requestInput.MaxResultCount));
    }

    private async void Submit(CreateProductRequest input)
    {
        // ✅ With success message notification
        Response result = await api.CallAsync(
            () => productsApi.CreateOrUpdateProductAsync(input),
            successMessage: "Product saved successfully");
        
        if (result is { IsError: false })
            dialogService.Close(true);
    }
}

// ❌ WRONG - Never call Refit directly
response = await productsApi.GetProductsAsync(...);  // NO! Missing error handling
```

### 6.2 ApiCallService Features

The `ApiCallService` provides:
- Automatic error handling and notifications
- Consistent response parsing
- Success message display
- HTTP exception handling

```csharp
// Full signature with all options
Response<T> result = await api.CallAsync<T>(
    () => refitApi.MethodAsync(),           // Required: API call
    errorTitle: "Custom Error Title",        // Optional: Override error title
    errorMessage: "Custom error message",    // Optional: Override error message
    successMessage: "Operation successful",  // Optional: Show success notification
    successTitle: "Success",                 // Optional: Override success title
    showErrorNotification: true              // Optional: Disable error notifications
);
```

### 6.3 Refit Interface Pattern
```csharp
// In Infrastructure/Refit/IProductsApi.cs
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Products;
using Refit;

namespace Krafter.UI.Web.Client.Infrastructure.Refit;

public interface IProductsApi
{
    // Use [Query] with GetRequestInput - Refit serializes all properties as query params
    // This matches backend's [AsParameters] GetRequestInput pattern
    [Get("/products/get")]
    Task<Response<PaginationResponse<ProductDto>>> GetProductsAsync(
        [Query] GetRequestInput request,
        CancellationToken cancellationToken = default);

    [Post("/products/create-or-update")]
    Task<Response> CreateOrUpdateProductAsync(
        [Body] CreateProductRequest request,
        CancellationToken cancellationToken = default);

    [Post("/products/delete")]
    Task<Response> DeleteProductAsync(
        [Body] DeleteRequestInput request,
        CancellationToken cancellationToken = default);
}
```

### 6.4 Register in RefitServiceExtensions.cs
```csharp
// Add to RefitServiceExtensions.AddKrafterRefitClients()
services.AddRefitClient<IProductsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(placeholderUrl))
    .AddHttpMessageHandler(sp => new RefitTenantHandler(sp.GetRequiredService<TenantIdentifier>(), isBffClient: false))
    .AddHttpMessageHandler<RefitAuthHandler>();
```

### 6.5 External API Pattern (Third-Party APIs)
```csharp
// In Infrastructure/Refit/IWeatherApi.cs
using Refit;

namespace Krafter.UI.Web.Client.Infrastructure.Refit;

public interface IWeatherApi
{
    [Get("/{city}")]
    Task<WeatherResponse> GetWeatherAsync(string city, [Query] string format = "j1");
}

// Response models for external API (define in same file or separate)
public class WeatherResponse
{
    public CurrentCondition[]? current_condition { get; set; }
}
```

```csharp
// Register in RefitServiceExtensions.cs - NO auth/tenant handlers for external APIs
services.AddRefitClient<IWeatherApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://wttr.in"));
```

## 7. New Feature Checklist
1. [ ] Create `Features/<Feature>/` folder
2. [ ] Create `<Feature>s.razor` + `<Feature>s.razor.cs` (List page)
3. [ ] Create `CreateOrUpdate<Feature>.razor` + `.razor.cs` (Form dialog)
4. [ ] Add route constant to `Common/Constants/KrafterRoute.cs`:
   ```csharp
   public const string Products = "/products";
   ```
5. [ ] Add permissions to `Common/Permissions/KrafterPermissions.cs` (mirror backend)
6. [ ] Create Refit interface `Infrastructure/Refit/I<Feature>sApi.cs`:
   ```csharp
   public interface IProductsApi
   {
       [Get("/products/get")]
       Task<Response<PaginationResponse<ProductDto>>> GetProductsAsync(
           [Query] GetRequestInput request,
           CancellationToken cancellationToken = default);
       
       [Post("/products/create-or-update")]
       Task<Response> CreateOrUpdateProductAsync([Body] CreateProductRequest request, ...);
       
       [Post("/products/delete")]
       Task<Response> DeleteProductAsync([Body] DeleteRequestInput request, ...);
   }
   ```
7. [ ] Register Refit client in `Infrastructure/Refit/RefitServiceExtensions.cs`:
   ```csharp
   services.AddRefitClient<IProductsApi>(refitSettings)
       .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
       .AddHttpMessageHandler<RefitTenantHandler>()
       .AddHttpMessageHandler<RefitAuthHandler>();
   ```
8. [ ] Update `Infrastructure/Services/MenuService.cs`:
   ```csharp
   new Menu
   {
       Name = "Products",
       Path = KrafterRoute.Products,
       Icon = "inventory_2",
       Permission = KrafterPermission.NameFor(KrafterAction.View, KrafterResource.Products)
   }
   ```
9. [ ] Add `EntityKind` enum value in `Krafter.Shared/Common/Enums/EntityKind.cs`
10. [ ] Update `Common/Components/Dialogs/DeleteDialog.razor.cs`:
    - Add new Refit interface to constructor
    - Add case to switch statement for new entity
11. [ ] Test with `dotnet build` and browser


## 8. Existing Refit Interfaces

| Interface | Endpoints | Usage |
|-----------|-----------|-------|
| `IAuthApi` | `/tokens/create`, `/tokens/refresh`, `/external-auth/google` | Login, token refresh |
| `IUsersApi` | `/users/get`, `/users/create-or-update`, `/users/delete`, etc. | User management |
| `IRolesApi` | `/roles/get`, `/roles/create-or-update`, `/roles/delete`, etc. | Role management |
| `ITenantsApi` | `/tenants/get`, `/tenants/create-or-update`, `/tenants/delete` | Tenant management |
| `IAppInfoApi` | `/app-info` | Application info |

## 9. Key Imports (from _Imports.razor)

```razor
@* Shared contracts - use these for all DTOs *@
@using Krafter.Shared.Common.Models
@using Krafter.Shared.Contracts.Users
@using Krafter.Shared.Contracts.Roles
@using Krafter.Shared.Contracts.Tenants

@* Refit interfaces *@
@using Krafter.UI.Web.Client.Infrastructure.Refit
```

## 10. Type Mappings (Shared Contracts)

| Type | Namespace | Usage |
|------|-----------|-------|
| `Response<T>` | `Krafter.Shared.Common.Models` | API response wrapper |
| `PaginationResponse<T>` | `Krafter.Shared.Common.Models` | Paginated list response |
| `GetRequestInput` | `Krafter.Shared.Common.Models` | Standard query parameters |
| `DeleteRequestInput` | `Krafter.Shared.Common.Models` | Delete request with reason |
| `UserDto` | `Krafter.Shared.Contracts.Users` | User data transfer object |
| `RoleDto` | `Krafter.Shared.Contracts.Roles` | Role data transfer object |
| `TenantDto` | `Krafter.Shared.Contracts.Tenants` | Tenant data transfer object |
| `EntityKind` | `Krafter.Shared.Common.Enums` | Entity type enum for delete |

## 11. Edge Cases: Menu & Navigation

### 11.1 Add Menu Item for New Feature
Update `Infrastructure/Services/MenuService.cs`:

```csharp
private Menu[] allMenus = new[]
{
    // ... existing menus ...
    
    // Top-level menu item
    new Menu
    {
        Name = "Products",
        Path = KrafterRoute.Products,
        Icon = "inventory_2",  // Material icon name
        Permission = KrafterPermission.NameFor(KrafterAction.View, KrafterResource.Products),
        Title = "Product Management",
        Description = "Manage products, pricing, and inventory.",
        Tags = new[] { "products", "inventory", "catalog" }
    },
    
    // OR nested under a parent menu
    new Menu
    {
        Name = "Catalog",
        Icon = "store",
        Children = new[]
        {
            new Menu
            {
                Name = "Products",
                Path = KrafterRoute.Products,
                Icon = "inventory_2",
                Permission = KrafterPermission.NameFor(KrafterAction.View, KrafterResource.Products),
                Title = "Product Management",
                Description = "Manage products and pricing.",
                Tags = new[] { "products", "catalog" }
            },
            new Menu
            {
                Name = "Categories",
                Path = KrafterRoute.ProductCategories,
                Icon = "category",
                Permission = KrafterPermission.NameFor(KrafterAction.View, KrafterResource.ProductCategories),
                Title = "Category Management",
                Description = "Manage product categories.",
                Tags = new[] { "categories", "catalog" }
            }
        }
    }
};
```

### 11.2 Add Route Constant (UI)
Update `Common/Constants/KrafterRoute.cs`:

```csharp
namespace Krafter.UI.Web.Client.Common.Constants;

public static class KrafterRoute
{
    // ... existing routes ...
    public const string Products = "products";
    public const string ProductCategories = "product-categories";
}
```

### 11.3 Permissions (from Shared)
Permissions are defined in `Krafter.Shared.Common.Auth.Permissions/` and used by both Backend and UI:

| File | Location | Purpose |
|------|----------|---------|
| `KrafterAction.cs` | `Krafter.Shared` | Action constants (View, Create, Update, Delete) |
| `KrafterResource.cs` | `Krafter.Shared` | Resource constants (Users, Roles, Products) |
| `KrafterPermission.cs` | `Krafter.Shared` | Permission record with `NameFor()` helper |
| `KrafterPermissions.cs` | `Krafter.Shared` | All permissions collection |
| `MustHavePermissionAttribute.cs` | `UI.Web.Client` | Blazor page authorization attribute |

**When adding a new feature, update only Shared:**

```csharp
// src/Krafter.Shared/Common/Auth/Permissions/KrafterResource.cs
public const string Products = nameof(Products);

// src/Krafter.Shared/Common/Auth/Permissions/KrafterPermissions.cs
new("View Products", KrafterAction.View, KrafterResource.Products),
new("Create Products", KrafterAction.Create, KrafterResource.Products),
new("Update Products", KrafterAction.Update, KrafterResource.Products),
new("Delete Products", KrafterAction.Delete, KrafterResource.Products),
```

The UI imports permissions via `_Imports.razor`:
```razor
@using Krafter.Shared.Common.Auth.Permissions
@using Krafter.UI.Web.Client.Common.Permissions  @* Only for MustHavePermissionAttribute *@
```

## 12. Edge Cases: Delete Operations

### 12.1 Using CommonService.Delete
The `CommonService.Delete` method handles delete confirmation dialog:

```csharp
private async Task Delete(ProductDto input)
{
    if (response?.Data?.Items?.Contains(input) == true)
    {
        await commonService.Delete(
            new DeleteRequestInput
            {
                Id = input.Id,
                DeleteReason = input.DeleteReason,  // Optional reason
                EntityKind = EntityKind.Product     // Must match enum in Shared
            }, 
            $"Delete Product {input.Name}");  // Dialog title
    }
    else
    {
        grid.CancelEditRow(input);
        await grid.Reload();
    }
}
```

### 12.2 Add EntityKind for New Feature
Before using delete, ensure `EntityKind` enum has your entity in `src/Krafter.Shared/Common/Enums/EntityKind.cs`:

```csharp
public enum EntityKind
{
    // ... existing ...
    Product = 400,  // Add your entity
}
```

### 12.3 Update DeleteDialog for New Entity
When adding a new entity type, update `Common/Components/Dialogs/DeleteDialog.razor.cs` switch statement:

```csharp
case EntityKind.Product:
    Response productResult = await api.CallAsync(
        () => productsApi.DeleteProductAsync(
            new DeleteRequestInput
            {
                DeleteReason = deleteRequestInput.DeleteReason,
                Id = deleteRequestInput.Id,
                EntityKind = EntityKind.Product
            }),
        successMessage: "Product deleted successfully");

    result.IsError = productResult.IsError;
    result.StatusCode = productResult.StatusCode;
    result.Message = productResult.Message;
    break;
```

**IMPORTANT**: Also inject the new Refit interface in DeleteDialog constructor:
```csharp
public partial class DeleteDialog(
    DialogService dialogService,
    ApiCallService api,
    IUsersApi usersApi,
    IRolesApi rolesApi,
    ITenantsApi tenantsApi,
    IProductsApi productsApi  // Add new API interface
) : ComponentBase
```

## 13. Edge Cases: _Imports.razor Updates

When adding a new feature with new contracts, update `_Imports.razor`:

```razor
@* Add new contract namespace *@
@using Krafter.Shared.Contracts.Products
```

This makes `ProductDto`, `CreateProductRequest`, etc. available in all Blazor components without explicit `@using` statements.


## 14. LocalAppSate (Global UI State)

`LocalAppSate` is a static class that holds UI-wide state settings.

### 14.1 Setting Page Title
```csharp
protected override async Task OnInitializedAsync()
{
    LocalAppSate.CurrentPageTitle = "Products";  // Displayed in header/breadcrumb
    // ... rest of initialization
}
```

### 14.2 Available Properties
| Property | Type | Description |
|----------|------|-------------|
| `CurrentPageTitle` | `string` | Current page title for header |
| `Density` | `Density` | RadzenDataGrid density setting |
| `ShowCellDataAsTooltip` | `bool` | Show cell overflow as tooltip |
| `AllowColumnResize` | `bool` | Allow grid column resizing |

### 14.3 Usage in Components
```razor
<RadzenDataGrid 
    Density="LocalAppSate.Density" 
    ShowCellDataAsTooltip="LocalAppSate.ShowCellDataAsTooltip"
    AllowColumnResize="LocalAppSate.AllowColumnResize"
    ...>
```

## 15. Component Lifecycle Best Practices

### 15.1 Required Pattern for List Pages

```csharp
public partial class Products(
    CommonService commonService,
    DialogService dialogService,
    ApiCallService api,
    IProductsApi productsApi
) : ComponentBase, IDisposable  // MUST implement IDisposable
{
    protected override async Task OnInitializedAsync()
    {
        // 1. Set page title
        LocalAppSate.CurrentPageTitle = "Products";
        
        // 2. Subscribe to dialog close event
        dialogService.OnClose += Close;
        
        // 3. Load initial data
        await GetListAsync();
    }

    // Use dynamic type for result parameter
    private async void Close(dynamic result)
    {
        if (result == null || !result.Equals(true))
            return;
        await GetListAsync();
    }

    public void Dispose()
    {
        // CRITICAL: Always unsubscribe from events
        dialogService.OnClose -= Close;
        dialogService.Dispose();
    }
}
```

### 15.2 Common Mistakes

```csharp
// ❌ WRONG - Missing IDisposable
public partial class Products(...) : ComponentBase
{
    // Memory leak! Events not unsubscribed
}

// ❌ WRONG - Wrong Close signature
private async void Close(object? result)  // Should be dynamic
{
    if (result is not bool)  // This pattern doesn't work correctly
        return;
}

// ✅ CORRECT - Use dynamic type
private async void Close(dynamic result)
{
    if (result == null || !result.Equals(true))
        return;
    await GetListAsync();
}
```

## 16. GetRequestInput Standard Parameters

The `GetRequestInput` class from `Krafter.Shared.Common.Models` provides standard query parameters:

```csharp
public class GetRequestInput
{
    public string? Id { get; set; }           // Filter by specific ID
    public bool History { get; set; }          // Include historical records
    public bool IsDeleted { get; set; }        // Include soft-deleted records
    public string? Query { get; set; }         // Free-text search
    public string? Filter { get; set; }        // Dynamic LINQ filter
    public string? OrderBy { get; set; }       // Dynamic LINQ ordering
    public int SkipCount { get; set; } = 0;    // Pagination offset
    public int MaxResultCount { get; set; } = 10;  // Page size
}
```

### Usage in Refit Interface
```csharp
[Get("/products")]
Task<Response<PaginationResponse<ProductDto>>> GetProductsAsync(
    [Query] GetRequestInput request,
    CancellationToken cancellationToken = default);
```

## 18. Refit Route Patterns (CRITICAL)

### 18.1 Refit Limitation with Route Constants
Refit has a runtime limitation with interpolated strings containing route parameters. **Always use literal strings in Refit interfaces.**

```csharp
// ❌ WRONG - Refit can't match route parameters from interpolated constants
[Get($"/{KrafterRoute.Roles}/{RouteSegment.RolePermissions}")]
Task<Response<RoleDto>> GetRolePermissionsAsync(string roleId, ...);
// Error: "URL /roles/{roleId}/permissions has parameter roleid, but no method parameter matches"

// ✅ CORRECT - Use literal strings for routes with parameters
[Get("/roles/{roleId}/permissions")]
Task<Response<RoleDto>> GetRolePermissionsAsync(string roleId, ...);
```

### 18.2 When to Use What

| Route Type | Example | Approach |
|------------|---------|----------|
| No parameters | `/roles`, `/users` | Can use constants (but literal is safer) |
| With `{id}` parameter | `/roles/{id}` | **Must use literal string** |
| With named parameters | `/users/{userId}/roles` | **Must use literal string** |
| Nested resources | `/roles/{roleId}/permissions` | **Must use literal string** |

### 18.3 Complete Refit Interface Example
```csharp
// File: Infrastructure/Refit/IRolesApi.cs
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Roles;
using Refit;

namespace Krafter.UI.Web.Client.Infrastructure.Refit;

public interface IRolesApi
{
    // ✅ No route parameter - literal string
    [Get("/roles")]
    Task<Response<PaginationResponse<RoleDto>>> GetRolesAsync(
        [Query] GetRequestInput request,
        CancellationToken cancellationToken = default);

    // ✅ No route parameter - literal string
    [Post("/roles")]
    Task<Response> CreateOrUpdateRoleAsync([Body] CreateOrUpdateRoleRequest request,
        CancellationToken cancellationToken = default);

    // ✅ Route parameter {id} - literal string, parameter name matches
    [Delete("/roles/{id}")]
    Task<Response> DeleteRoleAsync(string id,
        CancellationToken cancellationToken = default);

    // ✅ Route parameter {roleId} - literal string, parameter name matches
    [Get("/roles/{roleId}/permissions")]
    Task<Response<RoleDto>> GetRolePermissionsAsync(string roleId,
        CancellationToken cancellationToken = default);

    // ✅ Body only, no route parameter
    [Put("/roles/permissions")]
    Task<Response> UpdateRolePermissionsAsync([Body] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default);
}
```

### 18.4 Route Constants Usage Summary

| Layer | Use `KrafterRoute`/`RouteSegment`? | Reason |
|-------|-----------------------------------|--------|
| Backend (Minimal APIs) | ✅ Yes | ASP.NET Core handles correctly |
| BFF (Program.cs) | ✅ Yes | Server-side ASP.NET |
| Refit Interfaces | ❌ No - Literal strings only | Refit runtime limitation |

### 18.5 Common Mistakes

```csharp
// ❌ WRONG - Using constants with route parameters
[Get($"/{KrafterRoute.Users}/{RouteSegment.UserRoles}")]
Task<Response<List<UserRoleDto>>> GetUserRolesAsync(string userId, ...);

// ❌ WRONG - Parameter name doesn't match route placeholder
[Delete("/users/{id}")]
Task<Response> DeleteUserAsync(string userId, ...);  // Should be 'id'

// ✅ CORRECT - Literal string, parameter name matches
[Delete("/users/{id}")]
Task<Response> DeleteUserAsync(string id, ...);

// ✅ CORRECT - Literal string, parameter name matches
[Get("/users/{userId}/roles")]
Task<Response<List<UserRoleDto>>> GetUserRolesAsync(string userId, ...);
```


---

## 17. Evolution & Maintenance

> **PARENT**: See also: [Root Agents.md](../../Agents.md) for global evolution rules.

### 17.1 When to UPDATE This File

| Trigger | Action |
|---------|--------|
| New component pattern discovered | Add to Section 4 Code Templates |
| ApiCallService signature changes | Update Section 6 immediately |
| New Refit interface added | Add to Section 8 Existing Refit Interfaces |
| LocalAppSate properties change | Update Section 14 |
| Dialog/form pattern changes | Update relevant template sections |
| AI agent makes repeated UI mistakes | Add to Section 15.2 Common Mistakes |

### 17.2 When to CREATE Child Agents.md

| Trigger | Location |
|---------|----------|
| A feature has 5+ unique UI patterns | `Features/<Feature>/Agents.md` |
| Common components grow complex | `Common/Components/Agents.md` |
| Infrastructure services have unique patterns | `Infrastructure/Agents.md` |
| Authentication UI becomes complex | `Features/Auth/Agents.md` |

### 17.3 Common Mistakes (AI Agents)

| Mistake | Correct Approach |
|---------|-----------------|
| Calling Refit directly without ApiCallService | Always use `api.CallAsync(() => refitApi.Method())` |
| Using `Close(object? result)` signature | Use `Close(dynamic result)` |
| Missing `IDisposable` on list pages | Always implement `IDisposable` and unsubscribe events |
| Forgetting `dialogService.OnClose += Close` | Subscribe in `OnInitializedAsync` |
| Not setting `LocalAppSate.CurrentPageTitle` | Set in `OnInitializedAsync` |
| Creating DTOs in UI project | Use DTOs from `Krafter.Shared.Contracts.*` |
| Missing permission attribute on page | Add `@attribute [MustHavePermission(...)]` |
| Passing individual query params to Refit Get methods | Use `[Query] GetRequestInput` - matches backend's `[AsParameters]` pattern |
| Using `KrafterRoute`/`RouteSegment` constants in Refit | Use literal strings - Refit has runtime issues with interpolated route parameters |
| Refit parameter name doesn't match route placeholder | `[Delete("/users/{id}")]` requires parameter named `id`, not `userId` |

---
Last Updated: 2026-01-03
Verified Against: Features/Users/Users.razor.cs, Features/Roles/Roles.razor.cs, Features/Tenants/Tenants.razor.cs, Infrastructure/Refit/IUsersApi.cs, Infrastructure/Refit/IRolesApi.cs, Infrastructure/Refit/ITenantsApi.cs, Infrastructure/Refit/IAuthApi.cs
---
