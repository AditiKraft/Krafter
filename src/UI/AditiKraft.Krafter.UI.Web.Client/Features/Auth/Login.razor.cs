using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Auth;

public partial class Login(
    IAuthenticationService authenticationService,
    AuthTokenService tokenService,
    NavigationManager navigationManager,
    ThemeManager themeManager,
    IConfiguration configuration,
    AppUrls appUrls,
    Blazored.SessionStorage.ISessionStorageService sessionStorage
) : ComponentBase
{
    [CascadingParameter] public Task<AuthenticationState> AuthState { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "ReturnUrl")]
    public string? ReturnUrl { get; set; }

    [CascadingParameter] public bool IsMobileDevice { get; set; }

    public bool isBusy { get; set; }
    public TokenRequest TokenRequestInput { get; set; } = new();
    private bool _shouldRedirect;

    protected override async Task OnInitializedAsync()
    {
        AuthenticationState authState = await AuthState;
        if (authState.User.Identity?.IsAuthenticated is true)
        {
            // A local JWT can remain unexpired after another tab/device revokes its session.
            // Revalidate at the login boundary before redirecting an apparently signed-in user.
            if (OperatingSystem.IsBrowser() && !await tokenService.SynchronizeFromServerAsync())
            {
                await authenticationService.LogoutAsync("Session no longer valid");
                return;
            }

            if (!string.IsNullOrWhiteSpace(LocalAppState.GoogleLoginReturnUrl) &&
                (string.IsNullOrWhiteSpace(ReturnUrl) || ReturnUrl == "/"))
            {
                ReturnUrl = LocalAppState.GoogleLoginReturnUrl;
                LocalAppState.GoogleLoginReturnUrl = "";
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
        if (_shouldRedirect)
        {
            _shouldRedirect = false;
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
            navigationManager.NavigateTo(SafeReturnUrl());
        }

        isBusy = false;
    }

    private readonly TwoFactorRequest verification = new();

    private async Task VerifyTwoFactor(TwoFactorRequest _)
    {
        isBusy = true;
        try
        {
            if (await authenticationService.VerifyTwoFactorAsync(verification.Code, verification.UseRecoveryCode))
                navigationManager.NavigateTo(SafeReturnUrl());
        }
        finally { verification.Code = string.Empty; isBusy = false; }
    }

    private void CancelVerification()
    {
        verification.Code = string.Empty;
        authenticationService.CancelTwoFactor();
    }

    private string SafeReturnUrl() => !string.IsNullOrEmpty(ReturnUrl) && ReturnUrl.StartsWith('/') &&
        !ReturnUrl.StartsWith("//", StringComparison.Ordinal) && !ReturnUrl.Contains('\\') &&
        !ReturnUrl.StartsWith("/login", StringComparison.OrdinalIgnoreCase) ? ReturnUrl : "/";

    private async Task StartGoogleLogin()
    {
        if (string.IsNullOrEmpty(ReturnUrl))
        {
            var uri = new Uri(navigationManager.Uri);
            ReturnUrl = uri.PathAndQuery;
        }

        string returnUrl = !string.IsNullOrEmpty(ReturnUrl) ? ReturnUrl : "";

        string host = new Uri(navigationManager.BaseUri).Host;

        string clientId = configuration["Authentication:Google:ClientId"] ?? "";
        string redirectUri = appUrls.GetGoogleRedirectUri().AbsoluteUri;

        string scope = "email profile";
        string responseType = "code";
        string nonce = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        string state = $"{Uri.EscapeDataString(host)}|||{Uri.EscapeDataString(SafeReturnUrl())}|||{nonce}";
        await sessionStorage.SetItemAsStringAsync("krafter-oauth-state", state);
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
