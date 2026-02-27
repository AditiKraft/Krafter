using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Users;

public partial class ResetPassword(
    NavigationManager navigationManager,
    ApiCallService api,
    IUsersApi usersApi
) : ComponentBase
{
    public ResetPasswordRequest ResetPasswordRequest { get; set; } = new();

    [SupplyParameterFromQuery(Name = "Token")]
    public string Token { get; set; } = default!;

    public bool IsBusy { get; set; }

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
            navigationManager.NavigateTo("/login");
        }
    }
}
