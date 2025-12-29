using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Tenants;
using Krafter.UI.Web.Client.Infrastructure.Refit;
using Mapster;

namespace Krafter.UI.Web.Client.Features.Tenants;

public partial class CreateOrUpdateTenant(
    DialogService dialogService,
    ITenantsApi tenantsApi
) : ComponentBase
{
    [Parameter] public TenantDto? TenantInput { get; set; } = new();
    private CreateOrUpdateTenantRequest CreateRequest = new();
    private bool isBusy = false;

    protected override async Task OnInitializedAsync()
    {
        if (TenantInput is not null)
        {
            CreateRequest = TenantInput.Adapt<CreateOrUpdateTenantRequest>();
        }
    }

    private async void Submit(CreateOrUpdateTenantRequest input)
    {
        if (TenantInput is not null)
        {
            isBusy = true;
            Response? result = await tenantsApi.CreateOrUpdateTenantAsync(input);
            isBusy = false;
            StateHasChanged();
            if (result is { IsError: false })
            {
                dialogService.Close(true);
            }
        }
        else
        {
            dialogService.Close(false);
        }
    }

    private void Cancel() => dialogService.Close(false);
}
