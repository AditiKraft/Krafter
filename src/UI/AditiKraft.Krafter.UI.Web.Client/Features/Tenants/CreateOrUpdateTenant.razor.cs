using AditiKraft.Krafter.Shared.Common.Models;
using AditiKraft.Krafter.Shared.Contracts.Tenants;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using Mapster;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Tenants;

public partial class CreateOrUpdateTenant(
    DialogService dialogService,
    ApiCallService api,
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
            Response result = await api.CallAsync(
                () => tenantsApi.CreateOrUpdateTenantAsync(input),
                successMessage: "Tenant saved successfully");
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
