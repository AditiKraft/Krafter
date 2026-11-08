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
    ILogger<AuthenticationService> logger
)
    : IAuthenticationService
{
    public event Action<string?>? LoginChange;

    public async Task<bool> LogoutAsync(string methodName)
    {
        ErrorMessage = null;
        logger.LogInformation("Logging out user via method: {MethodName}", methodName);
        try
        {
            await coordinator.ExecuteAsync(async () =>
            {
                // Finish in-flight refresh/current-token writes before clearing cookies and storage.
                // A failed BFF call must leave the current UI session available for a retry.
                await apiService.LogoutAsync(CancellationToken.None);
                if (formFactor.GetFormFactor() is "WebAssembly")
                {
                    await localStorage.ClearCacheAsync();
                }
                return true;
            });
        }
        catch (Exception exception)
        {
            logger.LogWarning("Unable to complete sign-out ({FailureType}).", exception.GetType().Name);
            ErrorMessage = "Unable to sign out. Please try again.";
            return false;
        }

        CancelTwoFactor();
        LoginChange?.Invoke(null);
        HandleNavigationToLogin(true);
        return true;
    }

    public TwoFactorChallengeResponse? PendingChallenge { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<bool> LoginAsync(TokenRequest model)
    {
        ErrorMessage = null;
        PendingChallenge = null;
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
        return await CompleteAsync(response.Data.Tokens);
    }

    public async Task<bool> VerifyTwoFactorAsync(string code, bool recovery)
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
        bool success = await CompleteAsync(response.Data);
        if (success) PendingChallenge = null;
        return success;
    }

    public void CancelTwoFactor() { PendingChallenge = null; ErrorMessage = null; }

    private async Task<bool> CompleteAsync(TokenResponse? token)
    {
        if (token is null || string.IsNullOrWhiteSpace(token.Token) || string.IsNullOrWhiteSpace(token.RefreshToken))
        { ErrorMessage = "Invalid sign-in response."; return false; }
        if (formFactor.GetFormFactor() is "WebAssembly") await localStorage.CacheAuthTokens(token);
        LoginChange?.Invoke("");
        layoutService.UpdateHeading(EventArgs.Empty);
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
