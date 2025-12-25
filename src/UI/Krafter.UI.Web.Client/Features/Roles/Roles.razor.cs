using Krafter.Api.Client;
using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client.Common.Constants;
using Krafter.UI.Web.Client.Common.Permissions;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Common.Enums;
using Krafter.UI.Web.Client.Common.Extensions;
using Krafter.UI.Web.Client.Infrastructure.Services;

namespace Krafter.UI.Web.Client.Features.Roles;

public partial class Roles(
    CommonService commonService,
    NavigationManager navigationManager,
    KrafterClient krafterClient,
    LayoutService layoutService,
    DialogService dialogService,
    NotificationService notificationService) : ComponentBase, IDisposable
{
    public const string RoutePath = KrafterRoute.Roles;
    private RadzenDataGrid<RoleDto> grid;
    private bool IsLoading = true;

    private GetRequestInput RequestInput = new();
    public string IdentifierBasedOnPlacement = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        IdentifierBasedOnPlacement = RequestInput.GetIdentifierBasedOnPlacement(nameof(Roles));

        LocalAppSate.CurrentPageTitle = $"Roles";

        dialogService.OnClose += Close;
        await GetListAsync();
    }

    private RoleDtoPaginationResponseResponse? response = new() { Data = new RoleDtoPaginationResponse() };

    private async Task GetListAsync(bool resetPaginationData = false)
    {
        IsLoading = true;
        if (resetPaginationData)
        {
            RequestInput.SkipCount = 0;
        }

        response = await krafterClient.Roles.GetPath.GetAsync(
            configuration =>
            {
                configuration.QueryParameters.Id = RequestInput.Id;
                configuration.QueryParameters.History = RequestInput.History;
                configuration.QueryParameters.IsDeleted = RequestInput.IsDeleted;
                configuration.QueryParameters.SkipCount = RequestInput.SkipCount;
                configuration.QueryParameters.MaxResultCount = RequestInput.MaxResultCount;
                configuration.QueryParameters.Filter = RequestInput.Filter;
                configuration.QueryParameters.OrderBy = RequestInput.OrderBy;
                configuration.QueryParameters.Query = RequestInput.Query;
            }, CancellationToken.None
        );

        IsLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task AddRole()
    {
        await dialogService.OpenAsync<CreateOrUpdateRole>($"Add New Role",
            new Dictionary<string, object> { { "UserDetails", new RoleDto() } },
            new DialogOptions { Width = "50vw", Resizable = true, Draggable = true, Top = "5vh" });
    }

    private async Task UpdateRole(RoleDto user)
    {
        await dialogService.OpenAsync<CreateOrUpdateRole>($"Update Role {user.Name}",
            new Dictionary<string, object> { { "UserDetails", user } },
            new DialogOptions { Width = "50vw", Resizable = true, Draggable = true, Top = "5vh" });
    }

    private async Task DeleteRole(RoleDto roleDto)
    {
        if (response.Data.Items.Contains(roleDto))
        {
            await commonService.Delete(
                new DeleteRequestInput
                {
                    Id = roleDto.Id, DeleteReason = roleDto.DeleteReason, EntityKind = (int)EntityKind.KrafterRole
                }, $"Delete Role {roleDto.Name}");
        }
        else
        {
            grid.CancelEditRow(roleDto);
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
        RequestInput.SkipCount = args.Skip ?? 0;
        RequestInput.MaxResultCount = args.Top ?? 10;
        RequestInput.Filter = args.Filter;
        RequestInput.OrderBy = args.OrderBy;
        await GetListAsync();
    }

    private async Task ActionClicked(RadzenSplitButtonItem? item, RoleDto data)
    {
        if (item is { Value: KrafterAction.Update })
        {
            await UpdateRole(data);
        }
        else if (item is { Value: KrafterAction.Create })
        {
            await AddRole();
        }
        else if (item is { Value: KrafterAction.Delete })
        {
            await DeleteRole(data);
        }
    }

    public void Dispose()
    {
        dialogService.OnClose -= Close;
        dialogService.Dispose();
    }
}
