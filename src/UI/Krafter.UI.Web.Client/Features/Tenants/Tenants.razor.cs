using Krafter.Api.Client;
using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client.Common.Constants;
using Krafter.UI.Web.Client.Common.Permissions;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Common.Enums;
using Krafter.UI.Web.Client.Infrastructure.Services;

namespace Krafter.UI.Web.Client.Features.Tenants;

public partial class Tenants(
    CommonService commonService,
    KrafterClient krafterClient,
    DialogService dialogService
) : ComponentBase, IDisposable
{
    public const string RoutePath = KrafterRoute.Tenants;
    private RadzenDataGrid<TenantDto> grid;
    private bool IsLoading = true;
    private GetRequestInput requestInput = new();

    private TenantDtoPaginationResponseResponse? response = new() { Data = new TenantDtoPaginationResponse() };

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

        response = await krafterClient.Tenants.GetPath.GetAsync(
            configuration =>
            {
                configuration.QueryParameters.Id = requestInput.Id;
                configuration.QueryParameters.History = requestInput.History;
                configuration.QueryParameters.IsDeleted = requestInput.IsDeleted;
                configuration.QueryParameters.SkipCount = requestInput.SkipCount;
                configuration.QueryParameters.MaxResultCount = requestInput.MaxResultCount;
                configuration.QueryParameters.Filter = requestInput.Filter;
                configuration.QueryParameters.OrderBy = requestInput.OrderBy;
                configuration.QueryParameters.Query = requestInput.Query;
            }, CancellationToken.None
        );
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
        if (response.Data.Items.Contains(input))
        {
            await commonService.Delete(
                new DeleteRequestInput
                {
                    Id = input.Id, DeleteReason = input.DeleteReason, EntityKind = (int)EntityKind.Tenant
                }, $"Delete Tenant {input.Name}");
        }
        else
        {
            grid.CancelEditRow(input);
            await grid.Reload();
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
