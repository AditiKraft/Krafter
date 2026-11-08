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
string nonce = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
string state = $"{Uri.EscapeDataString(host)}|||{Uri.EscapeDataString(SafeReturnUrl())}|||{nonce}";
await sessionStorage.SetItemAsStringAsync("krafter-oauth-state", state);
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

## Two-factor authentication
- MFA members of authentication interfaces are mandatory contracts. Production implementations supply them; unrelated test doubles explicitly declare unsupported MFA behavior.
- Pass caller cancellation through BFF and Refit calls. `AuthApiErrors` propagates an explicitly canceled caller token and converts dependency timeouts to an error envelope.
- Settings actions acquire the busy guard before showing a confirmation dialog, then release it in `finally`; keep setup/recovery material transient.
- `AuthenticationService` keeps pending challenges only in scoped flow memory. It persists tokens only after completed authentication; invalid credentials show a persistent error instead of forcing a reload.
- `Login.razor` handles authenticator/recovery verification. The Google callback returns to this flow when local MFA is required.
- `TwoFactorSettings.razor` uses `ITwoFactorApi` for management and holds setup/recovery responses only in transient component state. Do not prerender/persist these secrets, put them in URLs, or capture them in screenshots.
- `AuthCookieMiddleware` distinguishes primary `LoginResponse` from completed `TokenResponse`, including `/api/two-factor/login`. Pending responses must never write authentication cookies.
- `AuthTokenBffEndpoints` maps split-host primary proxies and shared current/logout routes; single-host selects only `MapClientSessionEndpoints` because backend operation routes are direct.
- `TwoFactorBffEndpoints` maps the split-host management/completion routes. Shared `BlazorJwtBearerEvents` validates the exact verified token through the API in split-host mode. Single-host composition supplies an in-process `SessionValidator` callback using the already resolved tenant; do not replace it with a loopback request that derives a new tenant from the connection hostname.
- Preserve safe local ReturnUrl values and session-bound OAuth state. Never accept arbitrary callback hosts or treat external login alone as verified local MFA.
- Use `Common/LocalReturnUrl.Normalize()` for every post-login target and decoded Google return path. Reject raw control characters as well as protocol-relative URLs and backslashes: browser parsing strips tabs/newlines and can interpret an apparently local path as an external URL. Preserve valid local query strings and fragments.
- Enrich server permission claims only when the cached credential exactly matches the verified JWT, including before reading fresh request items. Ignore unsigned permission cookies; header/query callers otherwise use backend authorization's database lookup.
- `AuthApiErrors` normalizes missing/invalid response envelopes and transport failures for generic and non-generic management responses. Use it for every management call, including disabling MFA. `ServerAuthApiErrors` additionally handles standard server resilience rejections without adding those dependencies to WebAssembly.

---
Last Updated: 2026-11-08
Verified Against: src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Auth/ServerAuthApiService.cs, src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Hosting/UiHostServiceRegistration.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/Login.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/GoogleCallback.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Auth/AuthenticationService.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/wwwroot/appsettings.json, src-single/UI/AditiKraft.Krafter.UI.Web.Client/wwwroot/appsettings.json, docs/url-configuration.md, src/AditiKraft.Krafter.Contracts/Common/AppUrls.cs, src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Hosting/UiUrlConfiguration.cs
---


