using Krafter.Api.Client;
using Krafter.Api.Client.Models;

namespace Krafter.UI.Web.Client.Features.Users;

public partial class ChangePassword(
    NavigationManager navigationManager,
    NotificationService notificationService,
    KrafterClient krafterClient
) : ComponentBase
{
    public ChangePasswordRequest ChangePasswordRequest { get; set; } = new();

    [SupplyParameterFromQuery(Name = "ReturnUrl")]
    public string ReturnUrl { get; set; }

    public bool IsBusy { get; set; }

    private async Task SubmitChangePassword(ChangePasswordRequest requestInput)
    {
        IsBusy = true;
        Response? response = await krafterClient.Users.ChangePassword.PostAsync(requestInput);
        IsBusy = false;
        if (response is { IsError: false })
        {
            notificationService.Notify(NotificationSeverity.Success, "Password Change",
                "Your password has been changed successfully");
            navigationManager.NavigateTo(!string.IsNullOrWhiteSpace(ReturnUrl) ? ReturnUrl : "/products");
        }
    }
}
