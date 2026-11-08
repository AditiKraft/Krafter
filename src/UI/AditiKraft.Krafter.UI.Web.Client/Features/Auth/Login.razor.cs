using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth.Common;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Auth;

public partial class Login(
    IAuthenticationService authenticationService,
    AuthTokenService tokenService,
    NavigationManager navigationManager,
    ThemeManager themeManager,
    AppUrls appUrls,
    Blazored.SessionStorage.ISessionStorageService sessionStorage
) : ComponentBase
{
    [CascadingParameter] public Task<AuthenticationState> AuthState { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "ReturnUrl")]
    public string? ReturnUrl { get; set; }

    [SupplyParameterFromQuery(Name = "error")]
    public string? Error { get; set; }

    private string? QueryErrorMessage => Error switch
    {
        "google-auth-failed" => "Unable to sign in with Google. Please try again.",
        "invalid-google-state" => "Google sign-in expired or could not be verified. Please try again.",
        _ => null
    };
    private string? DisplayError => authenticationService.ErrorMessage ?? QueryErrorMessage;

    [CascadingParameter] public bool IsMobileDevice { get; set; }

    public bool isBusy { get; set; }
    public TokenRequest TokenRequestInput { get; set; } = new();
    private bool _shouldRedirect;

    protected override async Task OnInitializedAsync()
    {
        AuthenticationState authState = await AuthState;
        if (QueryErrorMessage is not null)
        {
            // A failed provider round-trip must remain visible even if another session exists.
            return;
        }
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
            navigationManager.NavigateTo(SafeReturnUrl());
        }
    }

    private async Task CreateToken(TokenRequest loginArgs)
    {
        Error = null;
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
        Error = null;
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

    private string SafeReturnUrl() => LocalReturnUrl.Normalize(ReturnUrl);

    private async Task StartGoogleLogin()
    {
        Error = null;
        if (string.IsNullOrEmpty(ReturnUrl))
        {
            var uri = new Uri(navigationManager.Uri);
            ReturnUrl = uri.PathAndQuery;
        }

        string nonce = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        await sessionStorage.SetItemAsStringAsync("krafter-google-browser-nonce", nonce);
        string nonceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(nonce)));
        string origin = new Uri(navigationManager.BaseUri).GetLeftPart(UriPartial.Authority);
        string start = new Uri(appUrls.GetRootUiUri(), "/authentication/google/start").AbsoluteUri;
        navigationManager.NavigateTo(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(start,
            new Dictionary<string, string?> { ["origin"] = origin, ["returnUrl"] = SafeReturnUrl(), ["nonceHash"] = nonceHash }), true);
    }
}
