using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Users;
using Krafter.UI.Web.Client.Infrastructure.Refit;

namespace Krafter.UI.Web.Client.Features.Users;

public partial class ForgotPassword(NavigationManager navigationManager, IUsersApi usersApi) : ComponentBase
{
    public ForgotPasswordRequest ForgotPasswordRequest { get; set; } = new();
    public bool IsBusy { get; set; }
    public bool MailSent { get; set; }

    private async Task SendForgotPasswordMail(ForgotPasswordRequest requestInput)
    {
        IsBusy = true;
        Response? response = await usersApi.ForgotPasswordAsync(requestInput);
        if (response is not null && response.IsError == false)
        {
            MailSent = true;
        }

        IsBusy = false;
    }
}
