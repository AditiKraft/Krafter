# Auth UI Feature AI Instructions

> **SCOPE**: Login UI, Google external login, and auth state handling.
> **PARENT**: See also: ../../../Agents.md

## 1. Core Principles
- Use `IAuthenticationService` for login/logout/refresh; do not call `IAuthApi` directly.
- Preserve `ReturnUrl` during login and Google callback.
- Store Google return URL in `LocalAppSate.GoogleLoginReturnUrl`.

## 2. Decision Tree
- Standard login? Use `AuthenticationService.LoginAsync(TokenRequest)`.
- Google login? Redirect to Google OAuth URL and handle `/google-callback`.
- Already authenticated? Redirect to `ReturnUrl` or `/`.

## 3. Code Templates

### Start Google Login
```csharp
string? clientId = configuration["Authentication:Google:ClientId"];
string redirectUri = $"{navigationManager.BaseUri}google-callback";
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
3. After external login, navigate to the stored return URL or `/`.

## 5. Common Mistakes
- Calling `IAuthApi` directly instead of `IAuthenticationService`.
- Dropping `ReturnUrl` during Google login round-trip.

## 6. Evolution Triggers
- Auth service or token storage logic changes.
- External provider (Google) flow changes.

## References (real code)
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/Login.razor`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/Login.razor.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/GoogleCallback.razor.cs`
- `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/_Shared/AuthenticationService.cs`

---
Last Updated: 2026-01-25
Verified Against: Features/Auth/Login.razor.cs, Features/Auth/GoogleCallback.razor.cs, Features/Auth/_Shared/AuthenticationService.cs
---
