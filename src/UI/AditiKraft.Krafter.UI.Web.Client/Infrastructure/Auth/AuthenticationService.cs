using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.AspNetCore.Http;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public class AuthenticationService(
    IAuthApiService apiService,
    AuthTokenService tokenService,
    TokenRefreshCoordinator coordinator,
    LayoutService layoutService,
    IAuthStorageService localStorage,
    NavigationManager navigationManager,
    IHttpContextAccessor httpContextAccessor,
    IFormFactor formFactor,
    AccountSecurityNavigationGuard securityNavigationGuard,
    ILogger<AuthenticationService> logger
)
    : IAuthenticationService
{
    public event Action<string?>? LoginChange;

    public async Task<bool> LogoutAsync(string methodName, long? expectedSessionVersion = null)
    {
        logger.LogInformation("Logging out user via method: {MethodName}", methodName);
        long? notificationVersion = null;
        bool success = await coordinator.ExecuteAsync(async () =>
        {
            if (!CanClearSession(expectedSessionVersion))
            {
                return false;
            }
            if (expectedSessionVersion is null) ErrorMessage = null;
            try
            {
                // Finish in-flight refresh/current-token writes before clearing cookies and storage.
                // A failed BFF call must leave the current UI session available for a retry.
                await apiService.LogoutAsync(CancellationToken.None);
                if (!CanClearSession(expectedSessionVersion)) return false;
                if (formFactor.GetFormFactor() is "WebAssembly")
                {
                    await localStorage.ClearCacheAsync();
                }
                if (!CanClearSession(expectedSessionVersion)) return false;
                if (expectedSessionVersion is { } version)
                {
                    if (!coordinator.TryAdvanceSessionVersion(version)) return false;
                    notificationVersion = version + 1;
                }
                else
                {
                    coordinator.AdvanceSessionVersion();
                }
                CancelTwoFactor();
                return true;
            }
            catch (Exception exception)
            {
                logger.LogWarning("Unable to complete sign-out ({FailureType}).", exception.GetType().Name);
                if (!CanClearSession(expectedSessionVersion)) return false;
                ErrorMessage = "Unable to sign out. Please try again.";
                return false;
            }
        });
        if (!success || !CanClearSession(notificationVersion)) return false;

        LoginChange?.Invoke(null);
        if (!CanClearSession(notificationVersion)) return false;
        HandleNavigationToLogin(true);
        return true;
    }

    public TwoFactorChallengeResponse? PendingChallenge { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<bool> LoginAsync(TokenRequest model)
    {
        coordinator.AdvanceSessionVersion();
        bool success = await coordinator.ExecuteAsync(async () =>
        {
            ErrorMessage = null;
            PendingChallenge = null;
            // The exchange itself can write server cookies, so serialize it with refresh and logout too.
            Response<LoginResponse> response = model.IsExternalLogin
                ? await apiService.ExternalAuthAsync(model, CancellationToken.None)
                : await apiService.CreateTokenAsync(model, CancellationToken.None);
            if (response.IsError || response.Data is null)
            {
                ErrorMessage = response.Message ?? "Unable to sign in.";
                return false;
            }
            if (!response.Data.IsCompleted && !response.Data.RequiresTwoFactor)
            { ErrorMessage = "Invalid sign-in response."; return false; }
            if (response.Data.RequiresTwoFactor)
            {
                PendingChallenge = response.Data.Challenge;
                return false;
            }
            return await SaveTokensAsync(response.Data.Tokens);
        });
        if (success)
        {
            // Subscribers can request tokens again; never notify while holding the coordinator.
            LoginChange?.Invoke("");
            layoutService.UpdateHeading(EventArgs.Empty);
        }
        return success;
    }

    public async Task<bool> VerifyTwoFactorAsync(string code, bool recovery)
    {
        coordinator.AdvanceSessionVersion();
        bool success = await coordinator.ExecuteAsync(async () =>
        {
            ErrorMessage = null;
            if (PendingChallenge is null) { ErrorMessage = "Start sign-in again."; return false; }
            Response<TokenResponse> response = await apiService.CompleteTwoFactorAsync(new TwoFactorRequest
            { ChallengeId = PendingChallenge.Id, Code = code, UseRecoveryCode = recovery }, CancellationToken.None);
            if (response.IsError || response.Data is null)
            {
                ErrorMessage = response.Message ?? "Unable to verify this code.";
                return false;
            }
            bool completed = await SaveTokensAsync(response.Data);
            if (completed) PendingChallenge = null;
            return completed;
        });
        if (success)
        {
            LoginChange?.Invoke("");
            layoutService.UpdateHeading(EventArgs.Empty);
        }
        return success;
    }

    public void CancelTwoFactor() { PendingChallenge = null; ErrorMessage = null; }

    private bool CanClearSession(long? expectedSessionVersion) => expectedSessionVersion is null ||
        (coordinator.SessionVersion == expectedSessionVersion && PendingChallenge is null &&
            securityNavigationGuard.BlockingMessage is null);

    private async Task<bool> SaveTokensAsync(TokenResponse? token)
    {
        if (token is null || string.IsNullOrWhiteSpace(token.Token) || string.IsNullOrWhiteSpace(token.RefreshToken))
        { ErrorMessage = "Invalid sign-in response."; return false; }
        if (formFactor.GetFormFactor() is "WebAssembly") await localStorage.CacheAuthTokens(token);
        return true;
    }

    private void HandleNavigationToLogin(bool forceLoad = false)
    {
        try
        {
            navigationManager.NavigateTo("/login", forceLoad);
            return;
        }
        catch (InvalidOperationException)
        {
            logger.LogInformation("NavigationManager not available, attempting server-side redirect.");
        }

        if (formFactor.GetFormFactor() is not "WebAssembly")
        {
            HttpContext? httpContext = httpContextAccessor.HttpContext;
            if (httpContext != null && !httpContext.Response.HasStarted)
            {
                logger.LogInformation("Performing server-side redirect to login page.");
                httpContext.Response.Redirect("/login");
                return;
            }
        }

        logger.LogWarning("Unable to navigate to login - no navigation context available.");
    }

    public Task<bool> RefreshAsync() => tokenService.RefreshAsync();
}
