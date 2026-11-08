# Auth Feature AI Instructions

> **SCOPE**: Token issuance, refresh tokens, and external login.
> **PARENT**: See also: ../../Agents.md

## 1. Core Principles
- Use `ITokenService` for all token issuance.
- Validate credentials with `UserManager<ApplicationUser>`.
- Refresh flow validates the stored `UserRefreshToken` and expiry.
- External login creates a user if missing and assigns the Basic role.
- Google login and token exchange use `AppUrls.GetGoogleRedirectUri()` from `Urls:RootUiUrl`. Register that exact callback with Google; keep one callback source for both steps.
- Prefer `Response<T>` factory methods for errors.

## 2. Decision Tree
- Standard login? Use `Login` with `TokenRequest`.
- Refresh token? Use `RefreshToken` with `RefreshTokenRequest` and stored refresh token.
- Google login? Use `ExternalAuth` with `GoogleAuthRequest` and `GoogleAuthClient`.

## 3. Code Templates

### Refresh Token Validation
```csharp
UserRefreshToken? refreshToken = await applicationDbContext.UserRefreshTokens
    .FirstOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);

if (refreshToken is null ||
    refreshToken.RefreshToken != request.RefreshToken ||
    refreshToken.RefreshTokenExpiryTime <= DateTime.UtcNow)
{
    return Response<TokenResponse>.Unauthorized("Invalid or expired refresh token.");
}
```

### External Auth: Ensure Basic Role
```csharp
ApplicationRole? basic = await roleManager.FindByNameAsync(RoleConstants.Basic);
if (basic is null)
{
    return Response<TokenResponse>.NotFound("Basic Role Not Found.");
}
```

## 4. Checklist
1. Use `ApiRoutes.Tokens` for token endpoints.
2. Use `RouteSegment.Refresh` for refresh.
3. Use `ApiRoutes.ExternalAuth` + `RouteSegment.Google` for Google login.
4. Use `HttpContext.Connection.RemoteIpAddress` after trusted forwarded-header middleware. Never trust raw client-supplied forwarding text.

## 5. Common Mistakes
- Issuing tokens without validating user activation or confirmed email settings.
- Skipping refresh token expiry checks.
- Creating external-login users without assigning the Basic role.

## 6. Evolution Triggers
- Token service or JWT settings change.
- External provider flow changes.
- Refresh token storage schema changes.

## References (real code)
- `src/AditiKraft.Krafter.Backend/Features/Auth/Login.cs`
- `src/AditiKraft.Krafter.Backend/Features/Auth/RefreshToken.cs`
- `src/AditiKraft.Krafter.Backend/Features/Auth/ExternalLogin.cs`
- `src/AditiKraft.Krafter.Backend/Features/Auth/Common/TokenService.cs`
- `src/AditiKraft.Krafter.Backend/Features/Auth/Common/UserRefreshToken.cs`

## Two-factor authentication
- `Common/TwoFactorService.cs` owns enrollment, pending login, factor consumption and session revocation. Never issue tokens from a pending challenge.
- Primary password/Google routes return `Response<LoginResponse>`; only completed login/refresh returns real `TokenResponse` credentials. `ITokenService` requires explicit MFA proof when the user is enrolled.
- Account transitions run under PostgreSQL transaction-level advisory locks. Bind challenges and replay state to tenant/user and authentication version. Do not replace this with a process-local lock or cache get/remove.
- Preserve `auth_time` and `amr` on refresh. `SessionValidator` checks tenant, account eligibility, version and MFA assurance in access/refresh paths.
- Refresh resolves the account and its refresh credential by the verified immutable user ID. Email changes must never select a different account or transfer authentication/MFA claims.
- Realtime validation uses the middleware-resolved tenant in `HttpContext.Items`, then reloads tenant/account eligibility in a fresh scope. Long Polling clones the HTTP service scope; restore its validated tenant before joining the tenant group. Never use the cloned scope's root default as the resolved tenant.
- Authenticator secrets use Data Protection. Production enrollment requires a persistent key ring and key-encryption certificate. Never log secret-bearing payloads.
- Management endpoints require the non-simple CSRF header and exact browser origin; the split-host BFF applies the same rule.
- Per-client throttling depends on trusted proxy configuration in shared ServiceDefaults. The split-host BFF forwards its resolved client address; configure Backend to trust that UI server. Never clear proxy trust lists or key limits directly on untrusted headers.
- See `docs/two-factor-authentication.md` for key storage, recovery, API compatibility and test requirements. Passkeys are outside this feature's scope.

---
Last Updated: 2026-11-08
Verified Against: src/AditiKraft.Krafter.Contracts/Common/AppUrls.cs, tests/AditiKraft.Krafter.Tests/Configuration/GoogleCallbackTests.cs, src/AditiKraft.Krafter.Backend/Features/Auth/Login.cs, src/AditiKraft.Krafter.Backend/Features/Auth/RefreshToken.cs, src/AditiKraft.Krafter.Backend/Features/Auth/ExternalLogin.cs, src/AditiKraft.Krafter.Backend/Features/Auth/Common/TokenService.cs, src/AditiKraft.Krafter.Backend/Features/Auth/Common/UserRefreshToken.cs, src/AditiKraft.Krafter.Contracts/Common/ApiRoutes.cs, src/AditiKraft.Krafter.Contracts/Contracts/Roles/RoleConstants.cs
---


