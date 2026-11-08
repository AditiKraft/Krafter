using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Auth;

public partial class GoogleCallback(IAuthenticationService authenticationService, NavigationManager navigationManager,
    AditiKraft.Krafter.Contracts.Common.AppUrls urls, Blazored.SessionStorage.ISessionStorageService sessionStorage)
    : ComponentBase
{
    protected override async Task OnInitializedAsync()
    {
        Uri uri = navigationManager.ToAbsoluteUri(navigationManager.Uri);
        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("code", out StringValues code))
        {
            try
            {
                string returnUrl = "";
                string host = "";
                if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("state", out StringValues encodedState))
                {
                    string decoded = Uri.UnescapeDataString(encodedState!);
                    string[] parts = decoded.Split("|||");
                    host = parts[0];
                    returnUrl = parts.Length > 1 ? parts[1] : "";
                    bool allowedHost = host.Equals(urls.GetRootUiUri().Host, StringComparison.OrdinalIgnoreCase) ||
                        (!urls.IsInvalidUiTenantHost(host) && urls.GetUiTenantIdentifier(host) is not null);
                    if (!allowedHost) { navigationManager.NavigateTo("/login?error=invalid-google-state"); return; }
                    if (host != uri.Host)
                    {
                        uri = new UriBuilder(uri) { Host = host }.Uri;
                        navigationManager.NavigateTo(uri.ToString(), true);
                        return;
                    }
                }

                if (!OperatingSystem.IsBrowser())
                {
                    return;
                }

                if (host == uri.Host)
                {
                    string? expected = await sessionStorage.GetItemAsStringAsync("krafter-oauth-state");
                    string actual = QueryHelpers.ParseQuery(uri.Query)["state"].ToString();
                    if (string.IsNullOrEmpty(expected) || !string.Equals(expected, actual, StringComparison.Ordinal))
                    { navigationManager.NavigateTo("/login?error=invalid-google-state"); return; }
                    await sessionStorage.RemoveItemAsync("krafter-oauth-state");
                    if (string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith('/') || returnUrl.StartsWith("//", StringComparison.Ordinal) || returnUrl.Contains('\\'))
                        returnUrl = "/";
                    string ReturnUrl = returnUrl;
                    LocalAppState.GoogleLoginReturnUrl = ReturnUrl;
                    bool isSuccess = await authenticationService.LoginAsync(new TokenRequest
                    {
                        IsExternalLogin = true,
                        Code = code.ToString()
                    });
                    if (!isSuccess)
                    {
                        // Both pending MFA and ordinary errors return to login with scoped flow state intact.
                        navigationManager.NavigateTo("/login?ReturnUrl=" + Uri.EscapeDataString(ReturnUrl));
                        return;
                    }
                    if (isSuccess)
                    {
                        if (!string.IsNullOrWhiteSpace(ReturnUrl))
                        {
                            navigationManager.NavigateTo(ReturnUrl);
                        }
                        else
                        {
                            navigationManager.NavigateTo("/");
                        }
                    }
                }
                else
                {
                    await Task.Delay(1000);
                }
            }
            catch (Exception)
            {
                navigationManager.NavigateTo("/login?error=google-auth-failed");
            }
        }
        else
        {
            navigationManager.NavigateTo("/login?error=google-auth-failed");
        }
    }
}
