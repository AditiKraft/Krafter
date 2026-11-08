# Auth UI Feature AI Instructions

> **SCOPE**: Login UI, Google external login, and auth state handling.
> **PARENT**: See also: ../../../AGENTS.md

## 1. Core Principles
- Use `IAuthenticationService` for login/logout/refresh; do not call `IAuthApi` directly.
- `AuthTokenService` owns expiry checks, refresh, and initial token synchronization. Use `NeedsRefresh` for the shared one-minute refresh window. Check caller cancellation before saving a refresh response, including when an API implementation returns after cancellation.
- Keep token reads, refresh, and storage writes inside `TokenRefreshCoordinator`. It is shared across browser API clients and scoped on the server.
- Server HTTP-client scopes do not share that coordinator. `ServerAuthApiService` uses the shared `HybridCache` to combine refresh calls for the exact access/refresh token pair. Successful responses stay in local memory for five seconds so each request can save the same new cookies. Cache keys contain a hash, not credentials; refresh results never go to the distributed cache.
- Different token pairs refresh independently. Failed responses are not cached, and canceling one waiter does not cancel another waiting request. This coordination is within one UI server process; it is not a distributed lock.
- `AuthStorageService` saves browser tokens. `AuthStorageServiceServer` saves cookies and makes fresh values available to the current HTTP request.
- Treat cookie JWT parsing as best-effort cache-key extraction. Missing, malformed, and oversized cookies must not block sign-out. Clear request-local credentials as well as response cookies so later reads in the same request cannot restore old values; invalid or overflowing expiry cookies are expired.
- Preserve `ReturnUrl` during login and Google callback.
- Google sign-in must remain a named semantic button with keyboard activation and visible focus. Keep its light/dark image decorative; route both themes through the same existing nonce-bound `StartGoogleLogin` flow.
- Store Google return URL in `LocalAppState.GoogleLoginReturnUrl`.
- Use injected `AppUrls.GetGoogleRedirectUri()` for Google callbacks in every environment. Backend uses the same method for code exchange. Read [Configure application URLs](../../../../../docs/url-configuration.md) when changing callback or deployment settings.

## 2. Decision Tree
- Login pages and auth API contract? Use this feature folder (`IAuthApi.cs`).
- Login/logout orchestration, refresh, or token storage? Use `../../Infrastructure/Auth/`.
- Server cookies or JWT events? Use `../../../AditiKraft.Krafter.UI.Web/Infrastructure/Auth/`.
- Standard login? Use `AuthenticationService.LoginAsync(TokenRequest)`.
- Google login? Navigate through the root UI's `/authentication/google/start`; the server owns `/google-callback`. The browser redeems the proof-bound handoff at `/google-complete`.
- Already authenticated? Redirect to `ReturnUrl` or `/`.

## 3. Code Templates

### Start Google Login
```csharp
string nonce = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
await sessionStorage.SetItemAsStringAsync("krafter-google-browser-nonce", nonce);
string nonceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(nonce)));
string origin = new Uri(navigationManager.BaseUri).GetLeftPart(UriPartial.Authority);
string start = new Uri(appUrls.GetRootUiUri(), "/authentication/google/start").AbsoluteUri;
navigationManager.NavigateTo(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(start,
    new Dictionary<string, string?> { ["origin"] = origin, ["returnUrl"] = SafeReturnUrl(), ["nonceHash"] = nonceHash }), true);
```

### Google Callback Login
```csharp
bool isSuccess = await authenticationService.LoginAsync(new TokenRequest
{
    IsExternalLogin = true,
    GoogleHandoff = handoff,
    GoogleBrowserNonce = nonce
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
- `LogoutAsync` returns success only after server cookie removal and local cleanup. Serialize it with refresh/current-token writes through `TokenRefreshCoordinator`, releasing the coordinator before auth events or navigation. On failure, keep pending MFA/recovery state, display the fixed retryable `ErrorMessage`, and retry sign-out without replaying a completed security mutation.
- `Login.razor` handles authenticator/recovery verification. The Google callback returns to this flow when local MFA is required.
- `TwoFactorSettings.razor` uses `ITwoFactorApi` for management and holds setup/recovery responses only in transient component state. Do not prerender/persist these secrets, put them in URLs, or capture them in screenshots.
- `AuthCookieMiddleware` distinguishes primary `LoginResponse` from completed `TokenResponse`, including `/api/two-factor/login`. Pending responses must never write authentication cookies.
- `AuthTokenBffEndpoints` maps split-host primary proxies and shared current/logout routes; single-host selects only `MapClientSessionEndpoints` because backend operation routes are direct.
- Current-token GET responses contain credentials: set `Cache-Control: no-store` before reading storage, including error/challenge responses, even when no fresh cookie is written.
- Realtime notifications are optional. The client connects only for an authenticated display principal with `Notifications.View`; the hub still checks current database permissions. Keep startup failures contained, cancel superseded starts, release old connections, and unsubscribe login callbacks on component disposal. Transport logs must omit exception messages and URLs that may contain access tokens.
- Realtime uses `AuthTokenService` directly for cancellable refresh and must never trigger global logout on failure: that can discard pending or unsaved recovery material. Each connection owns a lifetime token used by startup and reconnect token acquisition; each acquisition also has a deadline and cancellable waits around storage/refresh APIs. SignalR's `StartAsync` token alone does not cancel its access-token callback. Cancel the connection lifetime before awaiting its disposal.
- `GoogleOAuthBffEndpoints` maps root start/callback in both hosts. The root correlation cookie is HttpOnly, Secure, host-only and SameSite Lax for the provider return. Backend owns shared expiring one-use state and enforced S256 PKCE. `/google-complete` receives only a short-lived handoff fragment; redemption also requires the initiating tab's nonce. Never move a raw Google code between hosts.
- A failed Google sign-in may return to the recorded canonical tenant only after state proof and tenant validation. Preserve the normalized ReturnUrl and show only allowlisted public error messages; invalid state remains at root.
- `TwoFactorBffEndpoints` maps the split-host management/completion routes. Shared `BlazorJwtBearerEvents` validates the exact verified token through the API in split-host mode. Single-host composition supplies an in-process `SessionValidator` callback using the already resolved tenant; do not replace it with a loopback request that derives a new tenant from the connection hostname.
- Preserve safe local ReturnUrl values and session-bound OAuth state. Never accept arbitrary callback hosts or treat external login alone as verified local MFA.
- Use `Common/LocalReturnUrl.Normalize()` for every post-login target and decoded Google return path. Reject raw control characters as well as protocol-relative URLs and backslashes: browser parsing strips tabs/newlines and can interpret an apparently local path as an external URL. Preserve valid local query strings and fragments.
- Enrich server permission claims only when the cached credential exactly matches the verified JWT, including before reading fresh request items. Ignore unsigned permission cookies; header/query callers otherwise use backend authorization's database lookup.
- `AuthApiErrors` normalizes missing/invalid response envelopes and transport failures for generic and non-generic management responses. Use it for every management call, including disabling MFA. `ServerAuthApiErrors` additionally handles standard server resilience rejections without adding those dependencies to WebAssembly.

---
Last Updated: 2026-11-08
Verified Against: src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Auth/ServerAuthApiService.cs, src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Hosting/UiHostServiceRegistration.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/Login.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/GoogleCallback.razor.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Auth/AuthenticationService.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/wwwroot/appsettings.json, src-single/UI/AditiKraft.Krafter.UI.Web.Client/wwwroot/appsettings.json, docs/url-configuration.md, src/AditiKraft.Krafter.Contracts/Common/AppUrls.cs, src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Hosting/UiUrlConfiguration.cs
---


