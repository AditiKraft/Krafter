# UI AI Instructions (Blazor + Radzen)

> **SCOPE**: Blazor pages, components, dialogs, services, API integration via Kiota.

## 1. Core Principles
- **Hybrid Blazor**: WebAssembly + Server rendering.
- **Code-Behind**: ALWAYS split `.razor` (markup) and `.razor.cs` (logic).
- **Feature-Based**: Group by feature, not by component type.
- **Kiota Client**: Use `KrafterClient` for ALL API calls. Never use raw `HttpClient`.

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
├── Client/                          ← Generated Kiota client
│   ├── KrafterClient.cs
│   └── Models/
├── Common/
│   ├── Components/Layout/
│   ├── Permissions/KrafterPermissions.cs
│   └── Constants/KrafterRoute.cs
└── Infrastructure/
    ├── Services/MenuService.cs
    └── Api/ClientSideApiService.cs
```

## 4. Code Templates

### 4.1 List Page - Markup (.razor)
```razor
@using Krafter.UI.Web.Client.Common.Permissions
@using Krafter.UI.Web.Client.Common.Constants
@attribute [Route(RoutePath)]
@attribute [MustHavePermission(KrafterAction.View, KrafterResource.Products)]

<AuthorizeView Policy="@KrafterPermission.NameFor(KrafterAction.Create, KrafterResource.Products)">
    <RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.End" class="mt-2 mb-4">
        <RadzenButton Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Primary" 
                      Icon="add_circle_outline" Text="Product" Click="@AddProduct"/>
    </RadzenStack>
</AuthorizeView>

<RadzenDataGrid @ref="grid" 
                IsLoading=@IsLoading 
                Count=@(response?.Data?.TotalCount ?? 0) 
                LoadData=@LoadData 
                AllowSorting="true" 
                Data="@response?.Data?.Items" 
                AllowFiltering="true" 
                AllowPaging="true" 
                PageSize="@requestInput.MaxResultCount">
    <Columns>
        <RadzenDataGridColumn Property="Name" Title="Name" />
        <RadzenDataGridColumn Property="Price" Title="Price" FormatString="{0:C}" />
        <RadzenDataGridColumn Title="Actions" Filterable="false" Sortable="false">
            <Template Context="data">
                <RadzenSplitButton Click=@(args => ActionClicked(args, data)) Text="Actions">
                    <ChildContent>
                        <AuthorizeView Policy="@KrafterPermission.NameFor(KrafterAction.Update, KrafterResource.Products)">
                            <RadzenSplitButtonItem Text="Edit" Value="@KrafterAction.Update" Icon="edit" />
                        </AuthorizeView>
                        <AuthorizeView Policy="@KrafterPermission.NameFor(KrafterAction.Delete, KrafterResource.Products)">
                            <RadzenSplitButtonItem Text="Delete" Value="@KrafterAction.Delete" Icon="delete" />
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
using Krafter.Api.Client;
using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client.Common.Constants;
using Krafter.UI.Web.Client.Common.Permissions;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Infrastructure.Services;
using Microsoft.Kiota.Abstractions;

namespace Krafter.UI.Web.Client.Features.Products;

public partial class Products(
    CommonService commonService,
    DialogService dialogService,
    LayoutService layoutService,
    KrafterClient krafterClient) : ComponentBase, IDisposable
{
    public const string RoutePath = KrafterRoute.Products;
    
    private RadzenDataGrid<ProductDto> grid = null!;
    private GetRequestInput requestInput = new();
    private ProductDtoPaginationResponseResponse? response = new()
    {
        Data = new ProductDtoPaginationResponse()
    };
    private bool IsLoading = true;

    protected override async Task OnInitializedAsync()
    {
        dialogService.OnClose += Close;
        layoutService.LocalAppSate.CurrentPageTitle = "Products";
        await GetListAsync();
    }

    private async Task LoadData(LoadDataArgs args)
    {
        IsLoading = true;
        await Task.Yield();
        requestInput.SkipCount = args.Skip ?? 0;
        requestInput.MaxResultCount = args.Top ?? 10;
        requestInput.Filter = args.Filter;
        requestInput.OrderBy = args.OrderBy;
        await GetListAsync();
    }

    private async Task GetListAsync(bool resetPagination = false)
    {
        IsLoading = true;
        if (resetPagination) requestInput.SkipCount = 0;
        
        response = await krafterClient.Products.Get.GetAsync(config =>
        {
            config.QueryParameters.SkipCount = requestInput.SkipCount;
            config.QueryParameters.MaxResultCount = requestInput.MaxResultCount;
            config.QueryParameters.Filter = requestInput.Filter;
            config.QueryParameters.OrderBy = requestInput.OrderBy;
        });
        
        IsLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task AddProduct()
    {
        await dialogService.OpenAsync<CreateOrUpdateProduct>("Add Product",
            new Dictionary<string, object> { { "ProductInput", new ProductDto() } },
            new DialogOptions { Width = "40vw", Resizable = true, Draggable = true });
    }

    private async Task UpdateProduct(ProductDto product)
    {
        await dialogService.OpenAsync<CreateOrUpdateProduct>($"Update {product.Name}",
            new Dictionary<string, object> { { "ProductInput", product } },
            new DialogOptions { Width = "40vw", Resizable = true, Draggable = true });
    }

    private async Task DeleteProduct(ProductDto product)
    {
        await commonService.Delete(new DeleteRequestInput
        {
            Id = product.Id,
            EntityKind = (int)EntityKind.Product
        }, $"Delete {product.Name}");
    }

    private async Task ActionClicked(RadzenSplitButtonItem? item, ProductDto data)
    {
        if (item?.Value == KrafterAction.Update) await UpdateProduct(data);
        else if (item?.Value == KrafterAction.Delete) await DeleteProduct(data);
    }

    private async void Close(dynamic result)
    {
        if (result is true) await GetListAsync();
    }

    public void Dispose() => dialogService.OnClose -= Close;
}
```

### 4.3 Form Dialog - Markup (.razor)
```razor
@using Krafter.Api.Client.Models

<RadzenTemplateForm Data="@CreateRequest" Submit="@Submit">
    <FluentValidationValidator Options="@(options => options.IncludeAllRuleSets())"/>
    <RadzenStack>
        <RadzenRow AlignItems="AlignItems.Center">
            <RadzenColumn Size="12" SizeMD="3">
                <RadzenLabel Text="Name" Component="Name" />
            </RadzenColumn>
            <RadzenColumn Size="12" SizeMD="9">
                <RadzenTextBox Style="width: 100%" @bind-Value="@CreateRequest.Name" Name="Name" />
            </RadzenColumn>
            <ValidationMessage For="@(() => CreateRequest.Name)" />
        </RadzenRow>
        
        <RadzenRow AlignItems="AlignItems.Center">
            <RadzenColumn Size="12" SizeMD="3">
                <RadzenLabel Text="Price" Component="Price" />
            </RadzenColumn>
            <RadzenColumn Size="12" SizeMD="9">
                <RadzenNumeric Style="width: 100%" @bind-Value="@CreateRequest.Price" Name="Price" />
            </RadzenColumn>
            <ValidationMessage For="@(() => CreateRequest.Price)" />
        </RadzenRow>
    </RadzenStack>
    
    <RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.Center" Gap="1rem" Class="rz-mt-8">
        <RadzenButton ButtonType="ButtonType.Submit" IsBusy="isBusy" Icon="save" Text="Save" />
        <RadzenButton ButtonStyle="ButtonStyle.Light" Icon="cancel" Text="Cancel" Click="@Cancel" />
    </RadzenStack>
</RadzenTemplateForm>
```

### 4.4 Form Dialog - Code-Behind (.razor.cs)
```csharp
using Krafter.Api.Client;
using Krafter.Api.Client.Models;
using Mapster;

namespace Krafter.UI.Web.Client.Features.Products;

public partial class CreateOrUpdateProduct(
    DialogService dialogService,
    KrafterClient krafterClient) : ComponentBase
{
    [Parameter] public ProductDto? ProductInput { get; set; }
    
    private CreateProductRequest CreateRequest = new();
    private bool isBusy;

    protected override void OnInitialized()
    {
        if (ProductInput is not null)
            CreateRequest = ProductInput.Adapt<CreateProductRequest>();
    }

    private async void Submit(CreateProductRequest input)
    {
        isBusy = true;
        var result = await krafterClient.Products.CreateOrUpdate.PostAsync(input);
        isBusy = false;
        StateHasChanged();
        
        if (result?.IsError == false)
            dialogService.Close(true);
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

## 6. API Calls via Kiota
```csharp
// ✅ CORRECT - Use KrafterClient
var response = await krafterClient.Products.Get.GetAsync(config =>
{
    config.QueryParameters.Filter = "active";
    config.QueryParameters.SkipCount = 0;
    config.QueryParameters.MaxResultCount = 10;
});

if (response?.IsError == false)
{
    products = response.Data.Items;
}

// ❌ WRONG - Never use HttpClient directly
var response = await httpClient.GetAsync("/products");
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
6. [ ] Update `Infrastructure/Services/MenuService.cs`:
   ```csharp
   new Menu
   {
       Name = "Products",
       Path = KrafterRoute.Products,
       Icon = "inventory_2",
       Permission = KrafterPermission.NameFor(KrafterAction.View, KrafterResource.Products)
   }
   ```
7. [ ] Regenerate Kiota client if API changed:
   ```bash
   cd src/UI/Krafter.UI.Web.Client && kiota update -o ./Client
   ```
8. [ ] Test with `dotnet build` and browser
