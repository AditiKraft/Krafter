using AditiKraft.Krafter.UI.Web.Client.Features.Auth._Shared;

namespace AditiKraft.Krafter.UI.Web.Client.Common.Components.Layout;

public partial class TopRight(
    IAuthenticationService authenticationService,
    NavigationManager navigationManager
) : ComponentBase
{
    [CascadingParameter] public bool IsMobileDevice { get; set; }

    [Parameter] public bool ShowProfileCard { get; set; }

    private async Task SplitButtonClick(RadzenSplitButtonItem? item)
    {
        if (item is { Value: "Logout" })
        {
            await authenticationService.LogoutAsync("SplitButtonClick 20");
            NavigateToLogin();
        }
        else if (item is { Value: "ChangePassword" })
        {
            navigationManager.NavigateTo(
                $"/account/change-password?ReturnUrl={navigationManager.ToBaseRelativePath(navigationManager.Uri)}");
        }
        else if (item is { Value: "Appearance" })
        {
            navigationManager.NavigateTo(
                $"/appearance");
        }
    }

    private void NavigateToLogin() => navigationManager.NavigateTo("/login");
}
