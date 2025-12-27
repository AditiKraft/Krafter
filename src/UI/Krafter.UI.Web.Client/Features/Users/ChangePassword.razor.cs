using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Users;
using Krafter.UI.Web.Client.Infrastructure.Refit;

namespace Krafter.UI.Web.Client.Features.Users;

public partial class ChangePassword(
    NavigationManager navigationManager,
    NotificationService notificationService,
    IUsersApi usersApi
) : ComponentBase
{
    public ChangePasswordRequest ChangePasswordRequest { get; set; } = new();

    [SupplyParameterFromQuery(Name = "ReturnUrl")]
    public string ReturnUrl { get; set; } = default!;

    public bool IsBusy { get; set; }

    private async Task SubmitChangePassword(ChangePasswordRequest requestInput)
    {
        IsBusy = true;
        Response? response = await usersApi.ChangePasswordAsync(requestInput);
        IsBusy = false;
        if (response is { IsError: false })
        {
            notificationService.Notify(NotificationSeverity.Success, "Password Change",
                "Your password has been changed successfully");
            navigationManager.NavigateTo(!string.IsNullOrWhiteSpace(ReturnUrl) ? ReturnUrl : "/products");
        }
    }
}
