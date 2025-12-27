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
    private CreateOrUpdateTenantRequest OriginalCreateRequest = new();
    private bool isBusy = false;
    public List<string> SelectedTables { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        if (TenantInput is not null)
        {
            CreateRequest = TenantInput.Adapt<CreateOrUpdateTenantRequest>();
            OriginalCreateRequest = TenantInput.Adapt<CreateOrUpdateTenantRequest>();
        }
    }

    private async void Submit(CreateOrUpdateTenantRequest input)
    {
        if (TenantInput is not null)
        {
            isBusy = true;
            CreateOrUpdateTenantRequest finalInput = new();
            if (string.IsNullOrWhiteSpace(input.Id))
            {
                if (string.IsNullOrWhiteSpace(input.Id))
                {
                    SelectedTables ??= new List<string>();
                    input.TablesToCopy = string.Join(",", SelectedTables);
                }

                finalInput = input;
            }
            else
            {
                finalInput.Id = input.Id;
                if (input.Name != OriginalCreateRequest.Name)
                {
                    finalInput.Name = input.Name;
                }

                if (input.Identifier != OriginalCreateRequest.Identifier)
                {
                    finalInput.Identifier = input.Identifier;
                }

                if (input.IsActive != OriginalCreateRequest.IsActive)
                {
                    finalInput.IsActive = input.IsActive;
                }

                if (input.ValidUpto != OriginalCreateRequest.ValidUpto)
                {
                    finalInput.ValidUpto = input.ValidUpto;
                }

                if (input.AdminEmail != OriginalCreateRequest.AdminEmail)
                {
                    finalInput.AdminEmail = input.AdminEmail;
                }
            }

            Response? result = await tenantsApi.CreateOrUpdateTenantAsync(finalInput);
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
