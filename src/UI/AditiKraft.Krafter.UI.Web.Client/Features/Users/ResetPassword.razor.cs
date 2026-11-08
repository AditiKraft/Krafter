using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Users;

public partial class ResetPassword(
    IAuthenticationService authentication,
    ApiCallService api,
    IUsersApi usersApi
) : ComponentBase
{
    public ResetPasswordRequest ResetPasswordRequest { get; set; } = new();

    [SupplyParameterFromQuery(Name = "Token")]
    public string Token { get; set; } = default!;

    public bool IsBusy { get; set; }

    protected override void OnParametersSet() => ResetPasswordRequest.Token = Token;

    private async Task SubmitResetPassword(ResetPasswordRequest requestInput)
    {
        requestInput.Token = Token;
        IsBusy = true;
        Response response = await api.CallAsync(
            () => usersApi.ResetPasswordAsync(requestInput),
            successMessage: "Password reset successfully",
            successTitle: "Password Reset");
        IsBusy = false;
        if (response is { IsError: false })
        {
            await authentication.LogoutAsync("Password reset. Sign in again.");
        }
    }
}
