using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Roles._Shared;

public partial class SingleSelectRoleDropDownDataGrid(
    ApiCallService api,
    IRolesApi rolesApi
) : ComponentBase
{
    private RadzenDropDownDataGrid<string> dropDownGrid = null!;
    private Response<PaginationResponse<RoleDto>>? response;
    private bool IsLoading = true;
    private IEnumerable<RoleDto>? Data;
    [Parameter] public GetRequestInput GetRequestInput { get; set; } = new();

    [Parameter] public string? Value { get; set; }

    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    [Parameter] public List<string> IdsToDisable { get; set; } = new();

    private async Task LoadData(LoadDataArgs args)
    {
        IsLoading = true;
        await Task.Yield();
        GetRequestInput.SkipCount = args.Skip ?? 0;
        GetRequestInput.MaxResultCount = args.Top ?? 10;
        GetRequestInput.Filter = args.Filter;
        GetRequestInput.OrderBy = args.OrderBy;
        IsLoading = true;
        response = await api.CallAsync(() => rolesApi.GetRolesAsync(GetRequestInput), showErrorNotification: true);
        if (response is { Data.Items: not null })
        {
            Data = response.Data.Items.Where(c => !IdsToDisable.Contains(c.Id)).ToList();
        }

        IsLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    private int GetCount()
    {
        if (response is { Data: { Items: not null, TotalCount: var totalCount } })
        {
            return totalCount;
        }

        return 0;
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
