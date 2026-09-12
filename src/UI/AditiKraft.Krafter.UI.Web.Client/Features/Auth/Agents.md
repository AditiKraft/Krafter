# Auth UI Feature AI Instructions

> **SCOPE**: Login UI, Google external login, and auth state handling.
> **PARENT**: See also: ../../../Agents.md

## 1. Core Principles
- Use `IAuthenticationService` for login/logout/refresh; do not call `IAuthApi` directly.
- `AuthTokenService` owns expiry checks, refresh, and initial token synchronization. Use `NeedsRefresh` for the shared one-minute refresh window.
- Keep token reads, refresh, and storage writes inside `TokenRefreshCoordinator`. It is shared across browser API clients and scoped on the server.
- Server HTTP-client scopes do not share that coordinator. `ServerAuthApiService` uses the shared `HybridCache` to combine refresh calls for the exact access/refresh token pair. Successful responses stay in local memory for five seconds so each request can save the same new cookies. Cache keys contain a hash, not credentials; refresh results never go to the distributed cache.
- Different token pairs refresh independently. Failed responses are not cached, and canceling one waiter does not cancel another waiting request. This coordination is within one UI server process; it is not a distributed lock.
- `AuthStorageService` saves browser tokens. `AuthStorageServiceServer` saves cookies and makes fresh values available to the current HTTP request.
- Preserve `ReturnUrl` during login and Google callback.
- Store Google return URL in `LocalAppState.GoogleLoginReturnUrl`.
- Use injected `AppUrls.GetGoogleRedirectUri()` for Google callbacks in every environment. Backend uses the same method for code exchange. Read [Configure application URLs](../../../../../docs/url-configuration.md) when changing callback or deployment settings.

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
string clientId = configuration["Authentication:Google:ClientId"] ?? "";
string redirectUri = appUrls.GetGoogleRedirectUri().AbsoluteUri;
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
3. Configure `Urls:RootUiUrl` on UI.Web and also on Backend in split-host mode. Keep these values identical; the browser reads UI.Web's public configuration.
4. After external login, navigate to the stored return URL or `/`.

## 5. Common Mistakes
- Calling `IAuthApi` directly instead of `IAuthenticationService`.
- Dropping `ReturnUrl` during Google login round-trip.
- Building Google callbacks from the current tenant host or a separate redirect setting instead of `AppUrls.GetGoogleRedirectUri()`.

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
Last Updated: 2026-09-12
Verified Against: src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Auth/ServerAuthApiService.cs, src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Hosting/UiHostServiceRegistration.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/Login.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/GoogleCallback.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Auth/AuthenticationService.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/wwwroot/appsettings.json, src-single/UI/AditiKraft.Krafter.UI.Web.Client/wwwroot/appsettings.json, docs/url-configuration.md, src/AditiKraft.Krafter.Contracts/Common/AppUrls.cs, src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Hosting/UiUrlConfiguration.cs
---


