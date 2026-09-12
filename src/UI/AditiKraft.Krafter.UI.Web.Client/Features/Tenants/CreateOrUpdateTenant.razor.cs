using AditiKraft.Krafter.Contracts.Common;
using Mapster;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Tenants;

public partial class CreateOrUpdateTenant(
    DialogService dialogService,
    ApiCallService api,
    ITenantsApi tenantsApi) : ComponentBase
{
    [Parameter] public TenantDto? TenantInput { get; set; } = new();
    private CreateOrUpdateTenantRequest CreateRequest = new();
    private bool isBusy;
    private string? dateError;
    private bool IsRoot => TenantInput?.Id == DefaultTenantConstants.Identifier;

    protected override void OnInitialized()
    {
        CreateRequest = TenantInput?.Adapt<CreateOrUpdateTenantRequest>() ?? new();
        if (string.IsNullOrWhiteSpace(CreateRequest.Id))
        {
            CreateRequest.ValidUpto = null;
        }
    }

    private void ChangeLocalExpiry(DateTime? value)
    {
        if (IsRoot)
        {
            return;
        }

        dateError = null;
        // Keep the original Local value, including its daylight-saving offset, when unchanged.
        if (value == CreateRequest.ValidUpto)
        {
            return;
        }

        if (value is { } local && (TimeZoneInfo.Local.IsInvalidTime(local) || TimeZoneInfo.Local.IsAmbiguousTime(local)))
        {
            CreateRequest.ValidUpto = null;
            dateError = "This time is skipped or repeated when the clocks change. Choose another time.";
            return;
        }
        CreateRequest.ValidUpto = value;
    }

    private async Task SubmitAsync(CreateOrUpdateTenantRequest input)
    {
        if (isBusy || dateError is not null)
        {
            return;
        }

        isBusy = true;
        try
        {
            Response result = string.IsNullOrWhiteSpace(input.Id)
                ? await api.CallAsync(() => tenantsApi.CreateTenantAsync(input), successMessage: "Tenant created successfully")
                : await api.CallAsync(() => tenantsApi.UpdateTenantAsync(input.Id, input), successMessage: "Tenant updated successfully");
            if (!result.IsError)
            {
                dialogService.Close(true);
            }
        }
        finally
        {
            isBusy = false;
        }
    }

    private void Cancel() => dialogService.Close(false);
}
