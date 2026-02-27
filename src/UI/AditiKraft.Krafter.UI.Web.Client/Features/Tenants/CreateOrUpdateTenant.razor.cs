using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
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
            Response result;
            if (string.IsNullOrWhiteSpace(input.Id))
            {
                result = await api.CallAsync(
                    () => tenantsApi.CreateTenantAsync(input),
                    successMessage: "Tenant created successfully");
            }
            else
            {
                result = await api.CallAsync(
                    () => tenantsApi.UpdateTenantAsync(input.Id, input),
                    successMessage: "Tenant updated successfully");
            }
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
