using Krafter.Api.Client;
using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client.Common.Models;

namespace Krafter.UI.Web.Client.Features.Users._Shared;

public partial class SingleSelectUserDropDownDataGrid(
    KrafterClient krafterClient
) : ComponentBase
{
    private RadzenDropDownDataGrid<string> dropDownGrid;
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
            UserInfoPaginationResponseResponse? response = await krafterClient.Users.ByRole[RoleId]
                .GetAsync(configuration =>
                {
                    configuration.QueryParameters.Id = GetRequestInput.Id;
                    configuration.QueryParameters.History = GetRequestInput.History;
                    configuration.QueryParameters.IsDeleted = GetRequestInput.IsDeleted;
                    configuration.QueryParameters.SkipCount = GetRequestInput.SkipCount;
                    configuration.QueryParameters.MaxResultCount = GetRequestInput.MaxResultCount;
                    configuration.QueryParameters.Filter = GetRequestInput.Filter;
                    configuration.QueryParameters.OrderBy = GetRequestInput.OrderBy;
                    configuration.QueryParameters.Query = GetRequestInput.Query;
                });
            if (response is { Data.Items: not null })
            {
                Data = response.Data.Items.Where(c => !IdsToDisable.Contains(c.Id)).ToList();

                if (response.Data.TotalCount is { } totalCount)
                {
                    TotalCount = totalCount;
                }
            }
        }
        else
        {
            UserDtoPaginationResponseResponse? response = await krafterClient.Users.GetPath.GetAsync(configuration =>
            {
                configuration.QueryParameters.Id = GetRequestInput.Id;
                configuration.QueryParameters.History = GetRequestInput.History;
                configuration.QueryParameters.IsDeleted = GetRequestInput.IsDeleted;
                configuration.QueryParameters.SkipCount = GetRequestInput.SkipCount;
                configuration.QueryParameters.MaxResultCount = GetRequestInput.MaxResultCount;
                configuration.QueryParameters.Filter = GetRequestInput.Filter;
                configuration.QueryParameters.OrderBy = GetRequestInput.OrderBy;
                configuration.QueryParameters.Query = GetRequestInput.Query;
            }, CancellationToken.None);
            if (response is { Data.Items: not null })
            {
                if (response.Data.TotalCount is { } totalCount)
                {
                    TotalCount = totalCount;
                }

                Data = response.Data.Items.Where(c => !IdsToDisable.Contains(c.Id)).Select(c => new UserInfo
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
