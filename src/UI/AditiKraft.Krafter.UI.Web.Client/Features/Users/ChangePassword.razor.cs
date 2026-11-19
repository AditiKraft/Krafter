using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Users;

public partial class ChangePassword(
    IAuthenticationService authentication,
    ApiCallService api,
    IUsersApi usersApi
) : ComponentBase
{
    public ChangePasswordRequest ChangePasswordRequest { get; set; } = new();

    [SupplyParameterFromQuery(Name = "ReturnUrl")]
    public string ReturnUrl { get; set; } = default!;

    public bool IsBusy { get; set; }
    private bool passwordChanged;
    private string? signOutError;

    private async Task SubmitChangePassword(ChangePasswordRequest requestInput)
    {
        IsBusy = true;
        Response response = await api.CallAsync(
            () => usersApi.ChangePasswordAsync(requestInput),
            successMessage: "Your password has been changed successfully",
            successTitle: "Password Change");
        IsBusy = false;
        if (response is { IsError: false })
        {
            passwordChanged = true;
            await SignInAgainAsync();
        }
    }

    private async Task SignInAgainAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            signOutError = await authentication.LogoutAsync("Password changed. Sign in again.")
                ? null : authentication.ErrorMessage;
        }
        finally { IsBusy = false; }
    }
}
