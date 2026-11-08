using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.UI.Web.Client.Common.Components.Dialogs;
using Mapster;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Tenants;

public partial class CreateOrUpdateTenant(
    DialogService dialogService,
    ApiCallService api,
    ITenantsApi tenantsApi) : EditorDialogBase(dialogService)
{
    [Parameter] public TenantDto? TenantInput { get; set; } = new();
    private CreateOrUpdateTenantRequest CreateRequest = new();
    private string? dateError;
    private bool IsRoot => TenantInput?.Id == DefaultTenantConstants.Identifier;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        CreateRequest = TenantInput?.Adapt<CreateOrUpdateTenantRequest>() ?? new();
        if (string.IsNullOrWhiteSpace(CreateRequest.Id))
        {
            CreateRequest.ValidUpto = null;
        }
    }

    private void ChangeLocalExpiry(DateTime? value)
    {
        if (IsRoot || IsSaving || IsDisposed)
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
        if (dateError is not null || !TryBeginSave())
        {
            return;
        }

        CreateOrUpdateTenantRequest request = input.Adapt<CreateOrUpdateTenantRequest>();
        try
        {
            Response result = string.IsNullOrWhiteSpace(request.Id)
                ? await api.CallAsync(() => tenantsApi.CreateTenantAsync(request), successMessage: "Tenant created successfully")
                : await api.CallAsync(() => tenantsApi.UpdateTenantAsync(request.Id, request), successMessage: "Tenant updated successfully");
            if (!result.IsError)
            {
                CloseAfterSave();
            }
        }
        finally
        {
            EndSave();
        }
    }
}
