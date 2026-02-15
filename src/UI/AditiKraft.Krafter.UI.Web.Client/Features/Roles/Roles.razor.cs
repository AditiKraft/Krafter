using AditiKraft.Krafter.Shared.Common;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Roles;

public partial class Roles(
    NavigationManager navigationManager,
    ApiCallService api,
    IRolesApi rolesApi,
    LayoutService layoutService,
    DialogService dialogService) : ComponentBase, IDisposable
{
    public const string RoutePath = KrafterRoute.Roles;
    private RadzenDataGrid<RoleDto> grid = default!;
    private bool IsLoading = true;

    private AditiKraft.Krafter.Shared.Common.Models.GetRequestInput RequestInput = new();
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

        response = await api.CallAsync(() => rolesApi.GetRolesAsync(RequestInput));

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
        bool? confirmed = await dialogService.Confirm(
            $"Are you sure you want to delete role '{roleDto.Name}'?",
            "Delete Role",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirmed == true)
        {
            Response result = await api.CallAsync(
                () => rolesApi.DeleteRoleAsync(roleDto.Id),
                successMessage: "Role deleted successfully");

            if (!result.IsError)
            {
                await GetListAsync();
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
