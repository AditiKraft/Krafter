# Auth UI Feature AI Instructions

> **SCOPE**: Login UI, Google external login, and auth state handling.
> **PARENT**: See also: ../../../Agents.md

## 1. Core Principles
- Use `IAuthenticationService` for login/logout/refresh; do not call `IAuthApi` directly.
- `AuthTokenService` owns expiry checks, refresh, and initial token synchronization. Use `NeedsRefresh` for the shared one-minute refresh window.
- Keep token reads, refresh, and storage writes inside `TokenRefreshCoordinator`. It is shared across browser API clients and scoped on the server. Each caller checks its current token; refresh success is never shared through a process-wide timestamp.
- `AuthStorageService` saves browser tokens. `AuthStorageServiceServer` saves cookies and makes fresh values available to the current HTTP request.
- Preserve `ReturnUrl` during login and Google callback.
- Store Google return URL in `LocalAppState.GoogleLoginReturnUrl`.
- Use `RootUiUrl` from configuration for non-local Google callback redirects; do not hard-code production domains.

## 2. Decision Tree
- Login pages and auth API contract? Use this feature folder (`IAuthApi.cs`).
- Login/logout orchestration, refresh, or token storage? Use `../../Infrastructure/Auth/`.
- Server cookies or JWT events? Use `../../../AditiKraft.Krafter.UI.Web/Infrastructure/Auth/`.
- Standard login? Use `AuthenticationService.LoginAsync(TokenRequest)`.
- Google login? Redirect to Google OAuth URL and handle `/google-callback`.
- Already authenticated? Redirect to `ReturnUrl` or `/`.

## 3. Code Templates

### Start Google Login
```csharp
string? clientId = configuration["Authentication:Google:ClientId"];
string redirectUri = $"{navigationManager.BaseUri}google-callback";
if (!redirectUri.Contains("localhost"))
{
    string rootUiUrl = configuration["RootUiUrl"]
                       ?? throw new InvalidOperationException("RootUiUrl not configured");

    if (!Uri.TryCreate(rootUiUrl, UriKind.Absolute, out Uri? rootUiUri))
    {
        throw new InvalidOperationException("RootUiUrl must be an absolute URL");
    }

    redirectUri = $"{rootUiUri.AbsoluteUri.TrimEnd('/')}/google-callback";
}
string scope = "email profile";
string responseType = "code";
string state = $"{Uri.EscapeDataString(host)}|||{Uri.EscapeDataString(returnUrl)}";
string encodedState = Uri.EscapeDataString(state);

string authUrl = "https://accounts.google.com/o/oauth2/v2/auth?" +
                 $"client_id={Uri.EscapeDataString(clientId)}&" +
                 $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                 $"response_type={responseType}&" +
                 $"scope={Uri.EscapeDataString(scope)}&" +
                 $"state={encodedState}&" +
                 $"access_type=offline";

navigationManager.NavigateTo(authUrl, true);
```

### Google Callback Login
```csharp
bool isSuccess = await authenticationService.LoginAsync(new TokenRequest
{
    IsExternalLogin = true,
    Code = code.ToString()
});
```

## 4. Checklist
1. Use `@page "/login"` and `@page "/Account/Login"` routes.
2. Respect `ReturnUrl` query parameter.
3. Configure `RootUiUrl` in both split-host and single-host UI appsettings.
4. After external login, navigate to the stored return URL or `/`.

## 5. Common Mistakes
- Calling `IAuthApi` directly instead of `IAuthenticationService`.
- Dropping `ReturnUrl` during Google login round-trip.
- Hard-coding an external Google callback domain instead of using `RootUiUrl`.

## 6. Evolution Triggers
- Auth service or token storage logic changes.
- External provider (Google) flow changes.

## References (real code)
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/Login.razor`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/Login.razor.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/GoogleCallback.razor.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Auth/AuthenticationService.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Auth/AuthTokenService.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Auth/TokenRefreshCoordinator.cs`

---
Last Updated: 2026-09-08
Verified Against: src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/Login.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/GoogleCallback.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Auth/AuthenticationService.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/wwwroot/appsettings.json, src-single/UI/AditiKraft.Krafter.UI.Web.Client/wwwroot/appsettings.json
---


