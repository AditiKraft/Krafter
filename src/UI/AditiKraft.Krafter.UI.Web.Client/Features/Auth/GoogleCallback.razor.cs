using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth.Common;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using Microsoft.AspNetCore.WebUtilities;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Auth;

public partial class GoogleCallback(IAuthenticationService authenticationService, NavigationManager navigationManager,
    Blazored.SessionStorage.ISessionStorageService sessionStorage) : ComponentBase
{
    protected override async Task OnInitializedAsync()
    {
        if (!OperatingSystem.IsBrowser()) return;
        try
        {
            Uri uri = navigationManager.ToAbsoluteUri(navigationManager.Uri);
            var values = QueryHelpers.ParseQuery("?" + uri.Fragment.TrimStart('#'));
            string handoff = values["handoff"].ToString();
            string? nonce = await sessionStorage.GetItemAsStringAsync("krafter-google-browser-nonce");
            if (string.IsNullOrWhiteSpace(handoff) || string.IsNullOrWhiteSpace(nonce))
            { navigationManager.NavigateTo("/login?error=invalid-google-state", replace: true); return; }
            await sessionStorage.RemoveItemAsync("krafter-google-browser-nonce");
            string returnUrl = LocalReturnUrl.Normalize(values["returnUrl"].ToString());
            LocalAppState.GoogleLoginReturnUrl = returnUrl;
            bool complete = await authenticationService.LoginAsync(new TokenRequest
            { IsExternalLogin = true, GoogleHandoff = handoff, GoogleBrowserNonce = nonce });
            // Pending local MFA remains in the scoped authentication service when returning to login.
            navigationManager.NavigateTo(complete ? returnUrl : "/login?ReturnUrl=" + Uri.EscapeDataString(returnUrl), replace: true);
        }
        catch (Exception)
        {
            navigationManager.NavigateTo("/login?error=google-auth-failed", replace: true);
        }
    }
}
