using Krafter.Shared.Common;
using Krafter.Shared.Common.Auth.Permissions;
using Krafter.Shared.Common.Enums;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Users;
using Krafter.UI.Web.Client.Common.Constants;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Infrastructure.Refit;
using Krafter.UI.Web.Client.Infrastructure.Services;

namespace Krafter.UI.Web.Client.Features.Users;

public partial class Users(
    CommonService commonService,
    NavigationManager navigationManager,
    LayoutService layoutService,
    DialogService dialogService,
    ApiCallService api,
    IUsersApi usersApi
) : ComponentBase, IDisposable
{
    public const string RoutePath = KrafterRoute.Users;
    private RadzenDataGrid<UserDto> grid = default!;
    private Krafter.Shared.Common.Models.GetRequestInput requestInput = new();

    private Response<PaginationResponse<UserDto>>? response = new() { Data = new PaginationResponse<UserDto>() };

    private bool IsLoading = true;

    protected override async Task OnInitializedAsync()
    {
        dialogService.OnClose += Close;
        LocalAppSate.CurrentPageTitle = "Users";

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

    private async Task GetListAsync(bool resetPaginationData = false)
    {
        IsLoading = true;
        if (resetPaginationData)
        {
            requestInput.SkipCount = 0;
        }

        response = await api.CallAsync(() => usersApi.GetUsersAsync(
            requestInput.Id,
            requestInput.History,
            requestInput.IsDeleted,
            requestInput.Query,
            requestInput.Filter,
            requestInput.OrderBy,
            requestInput.SkipCount,
            requestInput.MaxResultCount));
        IsLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task AddUser()
    {
        await dialogService.OpenAsync<CreateOrUpdateUser>($"Add New User",
            new Dictionary<string, object> { { "UserInput", new UserDto() } },
            new DialogOptions { Width = "40vw", Resizable = true, Draggable = true, Top = "5vh" });
    }

    private async Task UpdateUser(UserDto user)
    {
        await dialogService.OpenAsync<CreateOrUpdateUser>($"Update User {user.FirstName}",
            new Dictionary<string, object> { { "UserInput", user } },
            new DialogOptions { Width = "40vw", Resizable = true, Draggable = true, Top = "5vh" });
    }

    private async Task DeleteUser(UserDto user)
    {
        if (response?.Data is not null && response.Data.Items is not null && response.Data.Items.Contains(user))
        {
            await commonService.Delete(
                new DeleteRequestInput
                {
                    Id = user.Id, DeleteReason = user.DeleteReason, EntityKind = EntityKind.KrafterUser
                }, $"Delete User {user.FirstName}");
        }
        else
        {
            grid.CancelEditRow(user);
            await grid.Reload();
        }
    }

    private async void Close(dynamic result)
    {
        if (result == null || !result.Equals(true))
        {
            return;
        }

        await GetListAsync();
    }

    private async Task ActionClicked(RadzenSplitButtonItem? item, UserDto data)
    {
        if (item is { Value: KrafterAction.Update })
        {
            await UpdateUser(data);
        }
        else if (item is { Value: KrafterAction.Create })
        {
            await AddUser();
        }
        else if (item is { Value: KrafterAction.Delete })
        {
            await DeleteUser(data);
        }
    }

    public void Dispose()
    {
        dialogService.OnClose -= Close;
        dialogService.Dispose();
    }
}
