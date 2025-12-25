using Krafter.Api.Client;
using Krafter.Api.Client.Models;

namespace Krafter.UI.Web.Client.Features.Users;

public partial class RestPassword(
    NavigationManager navigationManager,
    NotificationService notificationService,
    KrafterClient krafterClient
) : ComponentBase
{
    public ResetPasswordRequest ResetPasswordRequest { get; set; } = new();

    [SupplyParameterFromQuery(Name = "Token")]
    public string Token { get; set; }

    public bool IsBusy { get; set; }

    private async Task ResetPassword(ResetPasswordRequest requestInput)
    {
        requestInput.Token = Token;
        IsBusy = true;
        Response? response = await krafterClient.Users.ResetPassword.PostAsync(requestInput);
        IsBusy = false;
        if (response is { IsError: true })
        {
            notificationService.Notify(NotificationSeverity.Success, "Password Reset", "Password reset successfully");
            navigationManager.NavigateTo("/login");
        }
    }
}
