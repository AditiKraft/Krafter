using AditiKraft.Krafter.Shared.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth._Shared;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Auth;

public partial class Login(
    IAuthenticationService authenticationService,
    NavigationManager navigationManager,
    NotificationService notificationService,
    ThemeManager themeManager,
    IConfiguration configuration
) : ComponentBase
{
    [CascadingParameter] public Task<AuthenticationState> AuthState { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "ReturnUrl")]
    public string ReturnUrl { get; set; }

    [CascadingParameter] public bool IsMobileDevice { get; set; }

    public bool isBusy { get; set; }
    public TokenRequest TokenRequestInput { get; set; } = new();
    private bool _shouldRedirect;

    protected override async Task OnInitializedAsync()
    {
        AuthenticationState authState = await AuthState;
        if (authState.User.Identity?.IsAuthenticated is true)
        {
            if (!string.IsNullOrWhiteSpace(LocalAppSate.GoogleLoginReturnUrl) &&
                (string.IsNullOrWhiteSpace(ReturnUrl) || ReturnUrl == "/"))
            {
                ReturnUrl = LocalAppSate.GoogleLoginReturnUrl;
                LocalAppSate.GoogleLoginReturnUrl = "";
            }

            if (!string.IsNullOrWhiteSpace(ReturnUrl) &&
                (ReturnUrl.Contains("/login", StringComparison.InvariantCultureIgnoreCase)
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
            string finalReturnUrl = !string.IsNullOrWhiteSpace(ReturnUrl) ? ReturnUrl : "/";
            if (Uri.TryCreate(finalReturnUrl, UriKind.Absolute, out Uri? absoluteUri))
            {
                finalReturnUrl = absoluteUri.PathAndQuery;
            }

            navigationManager.NavigateTo(finalReturnUrl);
        }
    }

    private async Task CreateToken(TokenRequest loginArgs)
    {
        if (string.IsNullOrEmpty(ReturnUrl))
        {
            var uri = new Uri(navigationManager.Uri);
            ReturnUrl = uri.PathAndQuery;
        }

        isBusy = true;
        bool isSuccess = await authenticationService.LoginAsync(loginArgs);
        if (isSuccess)
        {
            if (!string.IsNullOrWhiteSpace(ReturnUrl) &&
                !ReturnUrl.Contains("/login", StringComparison.InvariantCultureIgnoreCase))
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

        string returnUrl = !string.IsNullOrEmpty(ReturnUrl) ? ReturnUrl : "";

        string host = new Uri(navigationManager.BaseUri).Host;

        string? clientId = configuration["Authentication:Google:ClientId"];
        string redirectUri = $"{navigationManager.BaseUri}google-callback";
        if (!redirectUri.Contains("localhost"))
        {
            redirectUri = $"https://krafter.getkrafter.dev/google-callback";
        }

        string scope = "email profile";
        string responseType = "code";
        string state = $"{Uri.EscapeDataString(host)}|||{Uri.EscapeDataString(returnUrl)}";
        string encodedState = Uri.EscapeDataString(state);

        string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                         $"client_id={Uri.EscapeDataString(clientId)}&" +
                         $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                         $"response_type={responseType}&" +
                         $"scope={Uri.EscapeDataString(scope)}&" +
                         $"state={encodedState}&" +
                         $"access_type=offline";

        navigationManager.NavigateTo(authUrl, true);
    }
}
