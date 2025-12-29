using Krafter.Shared.Common.Enums;
using Krafter.Shared.Common.Models;
using Krafter.UI.Web.Client.Infrastructure.Refit;
using Krafter.UI.Web.Client.Infrastructure.Services;

namespace Krafter.UI.Web.Client.Common.Components.Dialogs;

public partial class DeleteDialog(
    DialogService dialogService,
    ApiCallService api,
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
                    Response roleResult = await api.CallAsync(
                        () => rolesApi.DeleteRoleAsync(
                            new DeleteRequestInput
                            {
                                DeleteReason = deleteRequestInput.DeleteReason,
                                Id = deleteRequestInput.Id,
                                EntityKind = EntityKind.KrafterRole
                            }),
                        successMessage: "Role deleted successfully");

                    result.IsError = roleResult.IsError;
                    result.StatusCode = roleResult.StatusCode;
                    result.Message = roleResult.Message;
                    break;

                case EntityKind.KrafterUser:
                    Response userResult = await api.CallAsync(
                        () => usersApi.DeleteUserAsync(
                            new DeleteRequestInput
                            {
                                DeleteReason = deleteRequestInput.DeleteReason,
                                Id = deleteRequestInput.Id,
                                EntityKind = EntityKind.KrafterUser
                            }),
                        successMessage: "User deleted successfully");

                    result.IsError = userResult.IsError;
                    result.StatusCode = userResult.StatusCode;
                    result.Message = userResult.Message;
                    break;

                case EntityKind.Tenant:
                    Response tenantResult = await api.CallAsync(
                        () => tenantsApi.DeleteTenantAsync(
                            new DeleteRequestInput
                            {
                                DeleteReason = deleteRequestInput.DeleteReason,
                                Id = deleteRequestInput.Id,
                                EntityKind = EntityKind.Tenant
                            }),
                        successMessage: "Tenant deleted successfully");

                    result.IsError = tenantResult.IsError;
                    result.StatusCode = tenantResult.StatusCode;
                    result.Message = tenantResult.Message;
                    break;

                default:
                    isBusy = false;
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
