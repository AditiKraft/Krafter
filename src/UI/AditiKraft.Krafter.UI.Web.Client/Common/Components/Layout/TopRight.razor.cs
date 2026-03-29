using AditiKraft.Krafter.UI.Web.Client.Features.Auth.Common;

namespace AditiKraft.Krafter.UI.Web.Client.Common.Components.Layout;

public partial class TopRight(
    IAuthenticationService authenticationService,
    NavigationManager navigationManager
) : ComponentBase
{
    [CascadingParameter] public bool IsMobileDevice { get; set; }

    [Parameter] public bool ShowProfileCard { get; set; }

    [Parameter] public EventCallback OnNavigating { get; set; }

    private async Task SplitButtonClick(RadzenSplitButtonItem? item)
    {
        if (item is { Value: "Logout" })
        {
            await authenticationService.LogoutAsync("SplitButtonClick 20");
            await NavigateToLogin();
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

    private async Task NavigateToLogin()
    {
        await OnNavigating.InvokeAsync();
        navigationManager.NavigateTo("/login");
    }
}


