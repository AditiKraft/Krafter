using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Users;
using Krafter.UI.Web.Client.Infrastructure.Refit;
using Krafter.UI.Web.Client.Infrastructure.Services;

namespace Krafter.UI.Web.Client.Features.Users;

public partial class ChangePassword(
    NavigationManager navigationManager,
    ApiCallService api,
    IUsersApi usersApi
) : ComponentBase
{
    public ChangePasswordRequest ChangePasswordRequest { get; set; } = new();

    [SupplyParameterFromQuery(Name = "ReturnUrl")]
    public string ReturnUrl { get; set; } = default!;

    public bool IsBusy { get; set; }

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
            navigationManager.NavigateTo(!string.IsNullOrWhiteSpace(ReturnUrl) ? ReturnUrl : "/products");
        }
    }
}
