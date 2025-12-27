using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Users;
using Krafter.UI.Web.Client.Infrastructure.Refit;

namespace Krafter.UI.Web.Client.Features.Users;

public partial class RestPassword(
    NavigationManager navigationManager,
    NotificationService notificationService,
    IUsersApi usersApi
) : ComponentBase
{
    public ResetPasswordRequest ResetPasswordRequest { get; set; } = new();

    [SupplyParameterFromQuery(Name = "Token")]
    public string Token { get; set; } = default!;

    public bool IsBusy { get; set; }

    private async Task ResetPassword(ResetPasswordRequest requestInput)
    {
        requestInput.Token = Token;
        IsBusy = true;
        Response? response = await usersApi.ResetPasswordAsync(requestInput);
        IsBusy = false;
        if (response is { IsError: true })
        {
            notificationService.Notify(NotificationSeverity.Success, "Password Reset", "Password reset successfully");
            navigationManager.NavigateTo("/login");
        }
    }
}
