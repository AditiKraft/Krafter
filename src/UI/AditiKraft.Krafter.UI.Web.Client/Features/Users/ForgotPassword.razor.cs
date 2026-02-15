using AditiKraft.Krafter.Shared.Common.Models;
using AditiKraft.Krafter.Shared.Contracts.Users;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;

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
