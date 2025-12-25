using System.IdentityModel.Tokens.Jwt;
using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Infrastructure.Services;
using Krafter.UI.Web.Client.Infrastructure.Api;
using Krafter.UI.Web.Client.Infrastructure.Storage;
using Krafter.UI.Web.Client.Infrastructure.Http;
using Microsoft.AspNetCore.Http;

namespace Krafter.UI.Web.Client.Features.Auth._Shared;

public class AuthenticationService(
    IApiService apiService,
    LayoutService layoutService,
    IKrafterLocalStorageService localStorage,
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
        if (formFactor.GetFormFactor() is "WebAssembly")
        {
            await localStorage.ClearCacheAsync();
        }

        await apiService.LogoutAsync(CancellationToken.None);
        LoginChange?.Invoke("");
        await HandleNavigationToLogin(true);
    }

    public async Task<bool> LoginAsync(TokenRequestInput model)
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

        string? token = tokenResponse.Data.Token;
        string? refreshToken = tokenResponse.Data.RefreshToken;
        if (formFactor.GetFormFactor() is "WebAssembly")
        {
            await localStorage.CacheAuthTokens(tokenResponse.Data);
        }

        LoginChange?.Invoke("");
        layoutService.UpdateHeading(EventArgs.Empty);
        return true;
    }

    private bool IsTokenExpired(string? token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return true;
            }

            var handler = new JwtSecurityTokenHandler();
            if (handler.ReadToken(token) is JwtSecurityToken jwtToken)
            {
                return jwtToken.ValidTo <= DateTime.UtcNow.AddMinutes(1);
            }

            return true;
        }
        catch
        {
            return true;
        }
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

    public async Task<bool> RefreshAsync()
    {
        string? token = await localStorage.GetCachedAuthTokenAsync();
        string? refreshToken = await localStorage.GetCachedRefreshTokenAsync();
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(refreshToken))
        {
            await HandleNavigationToLogin();
            return false;
        }

        if (IsTokenExpired(token))
        {
            logger.LogInformation("Token expired, attempting to refresh.");
        }
        else
        {
            logger.LogInformation("Token is still valid, no need to refresh.");
            return true;
        }

        var model = new RefreshTokenRequest { Token = token, RefreshToken = refreshToken };

        Response<TokenResponse>? tokenResponse = null;
        bool synchronized = await TokenSynchronizationManager.TryExecuteWithSynchronizationAsync(
            async () =>
            {
                tokenResponse = await apiService.RefreshTokenAsync(model, CancellationToken.None);
                return tokenResponse;
            },
            r => r is not null && r.Data is not null && !r.IsError,
            logger,
            CancellationToken.None);

        if (tokenResponse is null || tokenResponse.Data is null || tokenResponse.IsError)
        {
            if (!IsTokenExpired(await localStorage.GetCachedAuthTokenAsync()))
            {
                return true;
            }

            return false;
        }

        if (formFactor.GetFormFactor() is "WebAssembly")
        {
            await localStorage.CacheAuthTokens(tokenResponse.Data);
        }

        return true;
    }
}

public class ErrorResponse
{
    public List<string> Messages { get; set; }
    public string Source { get; set; }
    public string Exception { get; set; }
    public string ErrorId { get; set; }
    public string SupportMessage { get; set; }
    public int StatusCode { get; set; }
}

public class ValidationErrorResponse
{
    public string Type { get; set; }
    public string Title { get; set; }
    public int Status { get; set; }
    public Dictionary<string, List<string>> Errors { get; set; }
}
