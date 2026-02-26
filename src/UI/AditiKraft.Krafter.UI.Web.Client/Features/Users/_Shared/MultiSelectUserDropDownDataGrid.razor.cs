using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Users._Shared;

public partial class MultiSelectUserDropDownDataGrid(
    ApiCallService api,
    IUsersApi usersApi
) : ComponentBase
{
    private RadzenDropDownDataGrid<IEnumerable<string>> _dropDownGrid = default!;
    private Response<PaginationResponse<UserDto>>? _response;
    private bool _isLoading = true;
    private IEnumerable<UserDto>? _data;
    [Parameter] public GetRequestInput GetRequestInput { get; set; } = new();

    private IEnumerable<string>? ValueEnumerable { get; set; }

    [Parameter] public List<string> Value { get; set; } = default!;

    [Parameter] public EventCallback<List<string>> ValueChanged { get; set; }

    protected override void OnParametersSet()
    {
        if (Value != null)
        {
            ValueEnumerable = Value;
        }
    }

    [Parameter] public List<string> IdsToDisable { get; set; } = new();

    private async Task LoadProcessesAsync(LoadDataArgs args)
    {
        _isLoading = true;
        await Task.Yield();
        GetRequestInput.SkipCount = args.Skip ?? 0;
        GetRequestInput.MaxResultCount = args.Top ?? 10;
        GetRequestInput.Filter = args.Filter;
        GetRequestInput.OrderBy = args.OrderBy;
        _isLoading = true;

        _response = await api.CallAsync(() => usersApi.GetUsersAsync(GetRequestInput), showErrorNotification: true);
        if (_response is { Data.Items: not null })
        {
            _data = _response.Data.Items.Where(c => !IdsToDisable.Contains(c.Id ?? "")).ToList();
        }

        _isLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    private int GetCount()
    {
        if (_response?.Data?.TotalCount is { } total)
        {
            return total;
        }

        return 0;
    }

    private async Task OnValueChangedAsync(object newValue)
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
