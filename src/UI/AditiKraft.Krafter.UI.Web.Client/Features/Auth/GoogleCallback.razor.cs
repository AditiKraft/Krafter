using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth.Common;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Auth;

public partial class GoogleCallback(IAuthenticationService authenticationService, NavigationManager navigationManager)
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
                    string ReturnUrl = returnUrl;
                    LocalAppSate.GoogleLoginReturnUrl = ReturnUrl;
                    bool isSuccess = await authenticationService.LoginAsync(new TokenRequest
                    {
                        IsExternalLogin = true, Code = code.ToString()
                    });
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Google login: {ex.Message}");
            }
        }
        else
        {
            navigationManager.NavigateTo("/login?error=google-auth-failed");
        }
    }
}


