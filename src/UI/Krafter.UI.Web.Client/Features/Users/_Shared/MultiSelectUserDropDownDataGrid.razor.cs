using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Users;
using Krafter.UI.Web.Client.Infrastructure.Refit;

namespace Krafter.UI.Web.Client.Features.Users._Shared;

public partial class MultiSelectUserDropDownDataGrid(
    IUsersApi usersApi
) : ComponentBase
{
    private RadzenDropDownDataGrid<IEnumerable<string>> dropDownGrid = default!;
    private Response<PaginationResponse<UserDto>>? response;
    private bool IsLoading = true;
    private IEnumerable<UserDto>? Data;
    [Parameter] public GetRequestInput GetRequestInput { get; set; } = new();

    private IEnumerable<string>? ValueEnumerable { get; set; }

    private List<string> _value = default!;

    [Parameter]
    public List<string> Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                ValueEnumerable = value;
            }
        }
    }

    [Parameter] public EventCallback<List<string>> ValueChanged { get; set; }

    [Parameter] public List<string> IdsToDisable { get; set; } = new();

    private async Task LoadProcesses(LoadDataArgs args)
    {
        IsLoading = true;
        await Task.Yield();
        GetRequestInput.SkipCount = args.Skip ?? 0;
        GetRequestInput.MaxResultCount = args.Top ?? 10;
        GetRequestInput.Filter = args.Filter;
        GetRequestInput.OrderBy = args.OrderBy;
        IsLoading = true;

        response = await usersApi.GetUsersAsync(
            GetRequestInput.Id,
            GetRequestInput.History,
            GetRequestInput.IsDeleted,
            GetRequestInput.Query,
            GetRequestInput.Filter,
            GetRequestInput.OrderBy,
            GetRequestInput.SkipCount,
            GetRequestInput.MaxResultCount);
        if (response is { Data.Items: not null })
        {
            Data = response.Data.Items.Where(c => !IdsToDisable.Contains(c.Id ?? "")).ToList();
        }

        IsLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    private int GetCount()
    {
        if (response?.Data?.TotalCount is { } total)
        {
            return total;
        }

        return 0;
    }

    private async Task OnValueChanged(object newValue)
    {
        if (newValue is IEnumerable<string> newValueEnumerable)
        {
            ValueEnumerable = newValueEnumerable;
            await ValueChanged.InvokeAsync(newValueEnumerable.ToList());
        }
        else
        {
            Console.WriteLine("Invalid value type");
        }
    }
}
