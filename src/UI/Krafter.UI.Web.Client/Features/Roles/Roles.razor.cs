using Krafter.Shared.Common.Auth.Permissions;
using Krafter.Shared.Common.Enums;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Roles;
using Krafter.UI.Web.Client.Common.Constants;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Infrastructure.Refit;
using Krafter.UI.Web.Client.Infrastructure.Services;

namespace Krafter.UI.Web.Client.Features.Roles;

public partial class Roles(
    CommonService commonService,
    NavigationManager navigationManager,
    IRolesApi rolesApi,
    LayoutService layoutService,
    DialogService dialogService,
    NotificationService notificationService) : ComponentBase, IDisposable
{
    public const string RoutePath = KrafterRoute.Roles;
    private RadzenDataGrid<RoleDto> grid = default!;
    private bool IsLoading = true;

    private Krafter.Shared.Common.Models.GetRequestInput RequestInput = new();
    public string IdentifierBasedOnPlacement = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        IdentifierBasedOnPlacement = nameof(Roles);

        LocalAppSate.CurrentPageTitle = $"Roles";

        dialogService.OnClose += Close;
        await GetListAsync();
    }

    private Response<PaginationResponse<RoleDto>>? response = new() { Data = new PaginationResponse<RoleDto>() };

    private async Task GetListAsync(bool resetPaginationData = false)
    {
        IsLoading = true;
        if (resetPaginationData)
        {
            RequestInput.SkipCount = 0;
        }

        response = await rolesApi.GetRolesAsync(
            RequestInput.Id,
            RequestInput.History,
            RequestInput.IsDeleted,
            RequestInput.Query,
            RequestInput.Filter,
            RequestInput.OrderBy,
            RequestInput.SkipCount,
            RequestInput.MaxResultCount);

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
        if (response?.Data?.Items?.Contains(roleDto) == true)
        {
            await commonService.Delete(
                new DeleteRequestInput
                {
                    Id = roleDto.Id, DeleteReason = roleDto.DeleteReason, EntityKind = EntityKind.KrafterRole
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
