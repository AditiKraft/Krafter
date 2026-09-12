using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.AspNetCore.Http;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public class AuthenticationService(
    IAuthApiService apiService,
    AuthTokenService tokenService,
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

    public async Task LogoutAsync(string methodName)
    {
        logger.LogInformation("Logging out user via method: {MethodName}", methodName);

        // Clear local storage first for WASM to ensure clean state before any events fire
        if (formFactor.GetFormFactor() is "WebAssembly")
        {
            await localStorage.ClearCacheAsync();
        }

        // apiService.LogoutAsync handles:
        // - WASM (ClientAuthApiService): Calls BFF /tokens/logout to clear HttpOnly cookies, then clears local storage
        // - Server (ServerAuthApiService): Clears cookies and hybrid cache directly
        await apiService.LogoutAsync(CancellationToken.None);

        LoginChange?.Invoke("");
        await HandleNavigationToLogin(true);
    }

    public async Task<bool> LoginAsync(TokenRequest model)
    {
        Response<TokenResponse>? tokenResponse;
        if (model is { IsExternalLogin: true })
        {
            tokenResponse = await apiService.ExternalAuthAsync(model, CancellationToken.None);
        }
        else
        {
            model.IsExternalLogin = false;
            tokenResponse = await apiService.CreateTokenAsync(model, CancellationToken.None);
        }

        if (tokenResponse is null || tokenResponse.Data is null || tokenResponse.IsError)
        {
            await LogoutAsync("AuthenticationService 55");
            return false;
        }

        if (formFactor.GetFormFactor() is "WebAssembly")
        {
            await localStorage.CacheAuthTokens(tokenResponse.Data);
        }

        LoginChange?.Invoke("");
        layoutService.UpdateHeading(EventArgs.Empty);
        return true;
    }

    private async Task HandleNavigationToLogin(bool forceLoad = false)
    {
        await localStorage.ClearCacheAsync();

        LoginChange?.Invoke(null);

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
