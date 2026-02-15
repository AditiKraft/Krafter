using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Users;

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
