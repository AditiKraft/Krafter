using Krafter.Shared.Common;
using Krafter.Shared.Common.Auth.Permissions;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Tenants;
using Krafter.UI.Web.Client.Common.Constants;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Infrastructure.Refit;
using Krafter.UI.Web.Client.Infrastructure.Services;

namespace Krafter.UI.Web.Client.Features.Tenants;

public partial class Tenants(
    ApiCallService api,
    ITenantsApi tenantsApi,
    DialogService dialogService
) : ComponentBase, IDisposable
{
    public const string RoutePath = KrafterRoute.Tenants;
    private RadzenDataGrid<TenantDto> grid = default!;
    private bool IsLoading = true;
    private Krafter.Shared.Common.Models.GetRequestInput requestInput = new();

    private Response<PaginationResponse<TenantDto>>? response = new() { Data = new PaginationResponse<TenantDto>() };

    protected override async Task OnInitializedAsync()
    {
        LocalAppSate.CurrentPageTitle = $"Tenants";

        dialogService.OnClose += Close;
        await Get();
    }

    private async Task Get(bool resetPaginationData = false)
    {
        IsLoading = true;
        if (resetPaginationData)
        {
            requestInput.SkipCount = 0;
        }

        response = await api.CallAsync(() => tenantsApi.GetTenantsAsync(requestInput));
        IsLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task Add()
    {
        await dialogService.OpenAsync<CreateOrUpdateTenant>($"Add New Tenant",
            new Dictionary<string, object> { { "TenantInput", new TenantDto() } },
            new DialogOptions { Width = "40vw", Resizable = true, Draggable = true, Top = "5vh" });
    }

    private async Task Update(TenantDto tenant)
    {
        await dialogService.OpenAsync<CreateOrUpdateTenant>($"Update Tenant {tenant.Name}",
            new Dictionary<string, object> { { "TenantInput", tenant } },
            new DialogOptions { Width = "40vw", Resizable = true, Draggable = true, Top = "5vh" });
    }

    private async Task Delete(TenantDto input)
    {
        bool? confirmed = await dialogService.Confirm(
            $"Are you sure you want to delete tenant '{input.Name}'?",
            "Delete Tenant",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirmed == true)
        {
            Response result = await api.CallAsync(
                () => tenantsApi.DeleteTenantAsync(input.Id),
                successMessage: "Tenant deleted successfully");

            if (!result.IsError)
            {
                await Get();
            }
        }
    }

    private async void Close(object? result)
    {
        if (result is not bool)
        {
            return;
        }

        await grid.Reload();
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

    private async Task ActionClicked(RadzenSplitButtonItem? item, TenantDto data)
    {
        if (item is { Value: KrafterAction.Update })
        {
            await Update(data);
        }
        else if (item is { Value: KrafterAction.Create })
        {
            await Add();
        }
        else if (item is { Value: KrafterAction.Delete })
        {
            await Delete(data);
        }
    }

    public void Dispose()
    {
        dialogService.OnClose -= Close;
        dialogService.Dispose();
    }
}
