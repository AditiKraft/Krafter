using Krafter.Shared.Common.Enums;
using Krafter.Shared.Common.Models;
using Krafter.UI.Web.Client.Infrastructure.Refit;

namespace Krafter.UI.Web.Client.Common.Components.Dialogs;

public partial class DeleteDialog(
    DialogService dialogService,
    NotificationService notificationService,
    IUsersApi usersApi,
    IRolesApi rolesApi,
    ITenantsApi tenantsApi
) : ComponentBase
{
    [Parameter] public DeleteRequestInput? DeleteRequestInput { get; set; } = new();

    private bool isBusy = false;

    private async Task Submit(DeleteRequestInput? deleteRequestInput)
    {
        if (deleteRequestInput is not null)
        {
            var result = new Response();

            isBusy = true;
            switch (deleteRequestInput.EntityKind)
            {
                case EntityKind.None:
                    break;

                case EntityKind.KrafterRole:
                    Response? roleResult = await rolesApi.DeleteRoleAsync(
                        new DeleteRequestInput
                        {
                            DeleteReason = deleteRequestInput.DeleteReason, 
                            Id = deleteRequestInput.Id,
                            EntityKind = EntityKind.KrafterRole
                        });

                    if (roleResult is not null)
                    {
                        result.IsError = roleResult.IsError;
                        result.StatusCode = roleResult.StatusCode;
                        result.Message = roleResult.Message;
                    }
                    break;

                case EntityKind.KrafterUser:
                    Response? userResult = await usersApi.DeleteUserAsync(
                        new DeleteRequestInput
                        {
                            DeleteReason = deleteRequestInput.DeleteReason, 
                            Id = deleteRequestInput.Id,
                            EntityKind = EntityKind.KrafterUser
                        });

                    if (userResult is not null)
                    {
                        result.IsError = userResult.IsError;
                        result.StatusCode = userResult.StatusCode;
                        result.Message = userResult.Message;
                    }
                    break;

                case EntityKind.Tenant:
                    Response? tenantResult = await tenantsApi.DeleteTenantAsync(
                        new DeleteRequestInput
                        {
                            DeleteReason = deleteRequestInput.DeleteReason, 
                            Id = deleteRequestInput.Id,
                            EntityKind = EntityKind.Tenant
                        });

                    if (tenantResult is not null)
                    {
                        result.IsError = tenantResult.IsError;
                        result.StatusCode = tenantResult.StatusCode;
                        result.Message = tenantResult.Message;
                    }
                    break;

                default:
                    isBusy = false;
                    notificationService.Notify(NotificationSeverity.Error, "Error", "Invalid Entity Kind");
                    return;
            }

            isBusy = false;
            StateHasChanged();
            if (!result.IsError)
            {
                dialogService.Close(true);
            }
        }
    }
}
