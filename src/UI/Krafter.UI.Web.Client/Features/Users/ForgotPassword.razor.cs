using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Users;
using Krafter.UI.Web.Client.Infrastructure.Refit;
using Krafter.UI.Web.Client.Infrastructure.Services;

namespace Krafter.UI.Web.Client.Features.Users;

public partial class ForgotPassword(ApiCallService api, IUsersApi usersApi) : ComponentBase
{
    public ForgotPasswordRequest ForgotPasswordRequest { get; set; } = new();
    public bool IsBusy { get; set; }
    public bool MailSent { get; set; }

    private async Task SendForgotPasswordMail(ForgotPasswordRequest requestInput)
    {
        IsBusy = true;
        Response response = await api.CallAsync(() => usersApi.ForgotPasswordAsync(requestInput));
        if (response is { IsError: false })
        {
            MailSent = true;
        }

        IsBusy = false;
    }
}
