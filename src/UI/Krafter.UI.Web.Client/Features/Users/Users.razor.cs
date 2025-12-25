using Krafter.Api.Client;
using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client.Common.Constants;
using Krafter.UI.Web.Client.Common.Permissions;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Common.Enums;
using Krafter.UI.Web.Client.Infrastructure.Services;
using Microsoft.Kiota.Abstractions;
using GetRequestBuilder = Krafter.Api.Client.Users.Get.GetRequestBuilder;


namespace Krafter.UI.Web.Client.Features.Users;

public partial class Users(
    CommonService commonService,
    NavigationManager navigationManager,
    LayoutService layoutService,
    DialogService dialogService,
    KrafterClient krafterClient
) : ComponentBase, IDisposable
{
    public const string RoutePath = KrafterRoute.Users;
    private RadzenDataGrid<UserDto> grid;
    private GetRequestInput requestInput = new();

    private UserDtoPaginationResponseResponse? response = new() { Data = new UserDtoPaginationResponse() };

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

        response = await krafterClient.Users.GetPath.GetAsync(RequestConfiguration(requestInput),
            CancellationToken.None);
        IsLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    private Action<RequestConfiguration<GetRequestBuilder.GetRequestBuilderGetQueryParameters>>? RequestConfiguration(
        GetRequestInput requestInput)
    {
        return configuration =>
        {
            configuration.QueryParameters.Id = requestInput.Id;
            configuration.QueryParameters.History = requestInput.History;
            configuration.QueryParameters.IsDeleted = requestInput.IsDeleted;
            configuration.QueryParameters.SkipCount = requestInput.SkipCount;
            configuration.QueryParameters.MaxResultCount = requestInput.MaxResultCount;
            configuration.QueryParameters.Filter = requestInput.Filter;
            configuration.QueryParameters.OrderBy = requestInput.OrderBy;
            configuration.QueryParameters.Query = requestInput.Query;
        };
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
                    Id = user.Id, DeleteReason = user.DeleteReason, EntityKind = (int)EntityKind.KrafterUser
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
