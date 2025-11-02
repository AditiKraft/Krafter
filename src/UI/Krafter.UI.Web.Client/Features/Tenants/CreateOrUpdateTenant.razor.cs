using Krafter.Api.Client;
using Krafter.Api.Client.Models;
using Mapster;

namespace Krafter.UI.Web.Client.Features.Tenants
{
    public partial class CreateOrUpdateTenant(
        DialogService dialogService,
        KrafterClient krafterClient
        ):ComponentBase
    {
        [Parameter] public TenantDto? TenantInput { get; set; } = new();
        CreateOrUpdateTenantRequestInput CreateRequest = new();
        CreateOrUpdateTenantRequestInput OriginalCreateRequest = new();
        private bool isBusy = false;
        public List<string> SelectedTables { get; set; } = new ();
        protected override async Task OnInitializedAsync()
        {
            if (TenantInput is { })
            {
                CreateRequest = TenantInput.Adapt<CreateOrUpdateTenantRequestInput>();
                OriginalCreateRequest = TenantInput.Adapt<CreateOrUpdateTenantRequestInput>();
            }
        }

        async void Submit(CreateOrUpdateTenantRequestInput input)
        {
            if (TenantInput is not null)
            {
                isBusy = true;
                CreateOrUpdateTenantRequestInput finalInput = new();
                if (string.IsNullOrWhiteSpace(input.Id))
                {
                    if (string.IsNullOrWhiteSpace(input.Id))
                    {
                        SelectedTables??=new List<string>();
                        input.TablesToCopy = string.Join(",", SelectedTables);;
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

                var result = await krafterClient.Tenants.CreateOrUpdate.PostAsync(finalInput);
                isBusy = false;
                StateHasChanged();
                if (result is{ IsError:false})
                {
                    dialogService.Close(true);
                }
            }
            else
            {
                dialogService.Close(false);
            }
        }

        void Cancel()
        {
            dialogService.Close(false);
        }
    }
}