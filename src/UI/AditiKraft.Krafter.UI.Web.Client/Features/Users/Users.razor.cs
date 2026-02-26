using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Users;

public partial class Users(
    NavigationManager navigationManager,
    LayoutService layoutService,
    DialogService dialogService,
    ApiCallService api,
    IUsersApi usersApi
) : ComponentBase, IDisposable
{
    public const string RoutePath = KrafterRoute.Users;
    private RadzenDataGrid<UserDto> grid = default!;
    private GetRequestInput requestInput = new();

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

        response = await api.CallAsync(() => usersApi.GetUsersAsync(requestInput));
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
            {
                await GetListAsync();
            }
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
