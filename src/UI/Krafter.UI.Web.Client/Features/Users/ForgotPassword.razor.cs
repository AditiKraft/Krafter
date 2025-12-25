using Krafter.Api.Client;
using Krafter.Api.Client.Models;

namespace Krafter.UI.Web.Client.Features.Users;

public partial class ForgotPassword(NavigationManager navigationManager, KrafterClient krafterClient) : ComponentBase
{
    public ForgotPasswordRequest ForgotPasswordRequest { get; set; } = new();
    public bool IsBusy { get; set; }
    public bool MailSent { get; set; }

    private async Task SendForgotPasswordMail(ForgotPasswordRequest requestInput)
    {
        IsBusy = true;
        Response? response = await krafterClient.Users.ForgotPassword.PostAsync(requestInput);
        if (response is not null && response.IsError == false)
        {
            MailSent = true;
        }

        IsBusy = false;
    }
}
