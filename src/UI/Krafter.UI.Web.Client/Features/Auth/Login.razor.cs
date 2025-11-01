using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client.Features.Auth._Shared;
using Krafter.UI.Web.Client.Infrastructure.Services;
using Krafter.UI.Web.Client.Common.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace Krafter.UI.Web.Client.Features.Auth;

public partial class Login(
    IAuthenticationService authenticationService,
    NavigationManager navigationManager,
    NotificationService notificationService,
    ThemeManager themeManager,
    IConfiguration configuration
    ) : ComponentBase
{
    [CascadingParameter]
    public Task<AuthenticationState> AuthState { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "ReturnUrl")]
    public string ReturnUrl { get; set; }

    [CascadingParameter]
    public bool IsMobileDevice { get; set; }

    public bool isBusy { get; set; }
    public TokenRequestInput TokenRequestInput { get; set; } = new();
    private bool _shouldRedirect;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthState;
        if (authState.User.Identity?.IsAuthenticated is true)
        {
            if (!string.IsNullOrWhiteSpace(LocalAppSate.GoogleLoginReturnUrl) && (string.IsNullOrWhiteSpace(ReturnUrl) || ReturnUrl == "/"))
            {
                ReturnUrl = LocalAppSate.GoogleLoginReturnUrl;
                LocalAppSate.GoogleLoginReturnUrl = "";
            }

            if (!string.IsNullOrWhiteSpace(ReturnUrl) && (ReturnUrl.Contains("/login", StringComparison.InvariantCultureIgnoreCase)
                || ReturnUrl.Contains("Account/Login", StringComparison.InvariantCultureIgnoreCase)))
            {
                ReturnUrl = "/";
            }
            _shouldRedirect = true;
        }
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && _shouldRedirect)
        {
            var finalReturnUrl = !string.IsNullOrWhiteSpace(ReturnUrl) ? ReturnUrl : "/";
            if (Uri.TryCreate(finalReturnUrl, UriKind.Absolute, out var absoluteUri))
            {
                finalReturnUrl = absoluteUri.PathAndQuery;
            }
            navigationManager.NavigateTo(finalReturnUrl);
        }
    }

    private async Task CreateToken(TokenRequestInput loginArgs)
    {
        if (string.IsNullOrEmpty(ReturnUrl))
        {
            var uri = new Uri(navigationManager.Uri);
            ReturnUrl = uri.PathAndQuery;
        }
        isBusy = true;
        var isSuccess = await authenticationService.LoginAsync(loginArgs);
        if (isSuccess)
        {
            if (!string.IsNullOrWhiteSpace(ReturnUrl) && !ReturnUrl.Contains("/login", StringComparison.InvariantCultureIgnoreCase))
            {
                navigationManager.NavigateTo(ReturnUrl);
            }
            else
            {
                navigationManager.NavigateTo("/");
            }
        }

        isBusy = false;
    }

    private void StartGoogleLogin()
    {
        if (string.IsNullOrEmpty(ReturnUrl))
        {
            var uri = new Uri(navigationManager.Uri);
            ReturnUrl = uri.PathAndQuery;
        }
        var returnUrl = !string.IsNullOrEmpty(ReturnUrl) ? ReturnUrl : "";

        var host = new Uri(navigationManager.BaseUri).Host;

        var clientId = configuration["Authentication:Google:ClientId"];
        var redirectUri = $"{navigationManager.BaseUri}google-callback";
        if (!redirectUri.Contains("localhost"))
        {
            redirectUri = $"https://krafter.getkrafter.dev/google-callback";
        }
        var scope = "email profile";
        var responseType = "code";
        var state = $"{Uri.EscapeDataString(host)}|||{Uri.EscapeDataString(returnUrl)}";
        var encodedState = Uri.EscapeDataString(state);

        var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                      $"client_id={Uri.EscapeDataString(clientId)}&" +
                      $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                      $"response_type={responseType}&" +
                      $"scope={Uri.EscapeDataString(scope)}&" +
                      $"state={encodedState}&" +
                      $"access_type=offline";

        navigationManager.NavigateTo(authUrl, forceLoad: true);
    }
}