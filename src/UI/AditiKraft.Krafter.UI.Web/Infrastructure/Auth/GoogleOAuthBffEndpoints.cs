using System.Security.Cryptography;
using System.Text;
using AditiKraft.Krafter.Contracts.Common;

namespace AditiKraft.Krafter.UI.Web.Infrastructure.Auth;

public static class GoogleOAuthBffEndpoints
{
    private const string CookiePrefix = "__Host-Krafter.Google.";

    public static void MapGoogleOAuthEndpoints(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/google-complete")) NoStore(context);
            await next(context);
        });
        app.MapGet("/authentication/google/start", StartAsync).AllowAnonymous();
        app.MapGet("/google-callback", CallbackAsync).AllowAnonymous();
    }

    private static async Task<IResult> StartAsync(HttpContext context, AppUrls urls, IGoogleOAuthApi api,
        string? origin, string? returnUrl, string? nonceHash, CancellationToken cancellationToken)
    {
        NoStore(context);
        if (!IsRootRequest(context, urls) || !IsSecret(nonceHash) || string.IsNullOrWhiteSpace(origin))
            return Results.BadRequest();
        string correlation = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        Response<GoogleLoginStartResponse> result = await ServerAuthApiErrors.CallAsync(() => api.StartAsync(new()
        {
            UiOrigin = origin, ReturnUrl = returnUrl ?? "/", ClientNonceHash = nonceHash!,
            RootCorrelationHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(correlation)))
        }, cancellationToken), cancellationToken);
        if (result.IsError || result.Data is null || !IsSecret(result.Data.State))
            return Results.Redirect("/login?error=google-auth-failed");
        context.Response.Cookies.Append(CookiePrefix + result.Data.State, correlation, Cookie(result.Data.ExpiresAt));
        return Results.Redirect(result.Data.AuthorizationUrl);
    }

    private static async Task<IResult> CallbackAsync(HttpContext context, AppUrls urls, IGoogleOAuthApi api,
        string? code, string? state, CancellationToken cancellationToken)
    {
        NoStore(context);
        if (!IsRootRequest(context, urls) || !IsSecret(state))
            return Results.Redirect("/login?error=invalid-google-state");
        string? correlation = context.Request.Cookies[CookiePrefix + state];
        context.Response.Cookies.Delete(CookiePrefix + state, Cookie(null));
        if (!IsSecret(correlation) || string.IsNullOrWhiteSpace(code))
            return Results.Redirect("/login?error=invalid-google-state");
        Response<GoogleLoginCallbackResponse> result = await ServerAuthApiErrors.CallAsync(() => api.CallbackAsync(new()
        { State = state!, Code = code, RootCorrelation = correlation! }, cancellationToken), cancellationToken);
        return result.IsError || result.Data is null
            ? Results.Redirect("/login?error=google-auth-failed")
            : Results.Redirect(result.Data.CompletionUrl);
    }

    private static bool IsRootRequest(HttpContext context, AppUrls urls) =>
        Uri.TryCreate($"{context.Request.Scheme}://{context.Request.Host}", UriKind.Absolute, out Uri? current) &&
        current.Scheme == urls.GetRootUiUri().Scheme && current.Port == urls.GetRootUiUri().Port &&
        string.Equals(current.IdnHost, urls.GetRootUiUri().IdnHost, StringComparison.OrdinalIgnoreCase);
    private static bool IsSecret(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    private static CookieOptions Cookie(DateTimeOffset? expires) => new()
    { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, IsEssential = true, Path = "/", Expires = expires };
    private static void NoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
