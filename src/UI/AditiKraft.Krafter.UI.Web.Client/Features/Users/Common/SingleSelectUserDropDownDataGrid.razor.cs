using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Users.Common;

public partial class SingleSelectUserDropDownDataGrid(
    ApiCallService api,
    IUsersApi usersApi
) : ComponentBase
{
    private RadzenDropDownDataGrid<string> dropDownGrid = default!;
    private int TotalCount = 0;
    private bool IsLoading = true;
    private IEnumerable<UserInfo>? Data;
    [Parameter] public GetRequestInput GetRequestInput { get; set; } = new();

    [Parameter] public string? Value { get; set; }

    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    [Parameter] public List<string> IdsToDisable { get; set; } = new();
    [Parameter] public string? RoleId { get; set; }

    private async Task LoadProcesses(LoadDataArgs args)
    {
        IsLoading = true;
        await Task.Yield();
        GetRequestInput.SkipCount = args.Skip ?? 0;
        GetRequestInput.MaxResultCount = args.Top ?? 10;
        GetRequestInput.Filter = args.Filter;
        GetRequestInput.OrderBy = args.OrderBy;
        IsLoading = true;
        if (!string.IsNullOrWhiteSpace(RoleId))
        {
            Response<PaginationResponse<UserInfo>> response = await api.CallAsync(() => usersApi.GetUsersByRoleAsync(
                RoleId, GetRequestInput), showErrorNotification: true);
            if (response is { Data.Items: not null })
            {
                Data = response.Data.Items.Where(c => !IdsToDisable.Contains(c.Id ?? "")).ToList();
                TotalCount = response.Data.TotalCount;
            }
        }
        else
        {
            Response<PaginationResponse<UserDto>> response = await api.CallAsync(() => usersApi.GetUsersAsync(
                GetRequestInput), showErrorNotification: true);
            if (response is { Data.Items: not null })
            {
                TotalCount = response.Data.TotalCount;
                Data = response.Data.Items.Where(c => !IdsToDisable.Contains(c.Id ?? "")).Select(c => new UserInfo
                {
                    Id = c.Id, FirstName = c.FirstName, LastName = c.LastName, CreatedOn = c.CreatedOn
                }).ToList();
            }
        }

        IsLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnValueChanged(object newValue)
    {
        if (newValue is string newValueEnumerable)
        {
            Value = newValueEnumerable;
            await ValueChanged.InvokeAsync(newValueEnumerable);
        }
        else
        {
            Console.WriteLine("Invalid value type");
        }
    }
}


