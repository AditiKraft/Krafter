namespace AditiKraft.Krafter.UI.Web.Client.Features.Roles.Common;

public partial class MultiSelectRoleDropDownDataGrid(
    ApiCallService api,
    IRolesApi rolesApi
) : ComponentBase, IDisposable
{
    private RadzenDropDownDataGrid<IEnumerable<string>> _dropDownGrid = null!;

    private Response<PaginationResponse<RoleDto>>? _response;
    private bool _isLoading = true;
    private bool disposed;
    private IEnumerable<RoleDto>? _data;
    [Parameter] public GetRequestInput GetRequestInput { get; set; } = new();

    private IEnumerable<string>? ValueEnumerable { get; set; }

    [Parameter] public List<string> Value { get; set; } = new();

    [Parameter] public bool Disabled { get; set; }

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
        GetRequestInput.Query = args.Filter;
        GetRequestInput.OrderBy = args.OrderBy;
        _isLoading = true;
        _response = await api.CallAsync(() => rolesApi.GetRolesAsync(GetRequestInput), showErrorNotification: true);
        if (disposed) return;
        if (_response is { Data.Items: not null })
        {
            _data = _response.Data.Items.Where(c => !IdsToDisable.Contains(c.Id)).ToList();
        }

        _isLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    private int GetCount()
    {
        if (_response is { Data: { Items: not null, TotalCount: var totalCount } })
        {
            return totalCount;
        }

        return 0;
    }

    private async Task OnValueChangedAsync(object? newValue)
    {
        if (Disabled || disposed) return;

        // Radzen's native Clear action emits null for IEnumerable<string>. Propagate an
        // empty selection so the parent can validate it instead of keeping stale roles.
        if (newValue is null)
        {
            ValueEnumerable = [];
            await ValueChanged.InvokeAsync([]);
        }
        else if (newValue is IEnumerable<string> newValueEnumerable)
        {
            ValueEnumerable = newValueEnumerable;
            await ValueChanged.InvokeAsync(newValueEnumerable.ToList());
        }
        else
        {
            Console.WriteLine("Invalid value type");
        }
    }

    public void Dispose()
    {
        disposed = true;
        GC.SuppressFinalize(this);
    }
}
