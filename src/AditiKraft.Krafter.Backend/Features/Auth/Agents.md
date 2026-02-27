# Auth Feature AI Instructions

> **SCOPE**: Token issuance, refresh tokens, and external login.
> **PARENT**: See also: ../../Agents.md

## 1. Core Principles
- Use `ITokenService` for all token issuance.
- Validate credentials with `UserManager<KrafterUser>`.
- Refresh flow validates the stored `UserRefreshToken` and expiry.
- External login creates a user if missing and assigns the Basic role.
- Prefer `Response<T>` factory methods for errors.

## 2. Decision Tree
- Standard login? Use `Login` with `TokenRequest`.
- Refresh token? Use `RefreshToken` with `RefreshTokenRequest` and stored refresh token.
- Google login? Use `ExternalAuth` with `GoogleAuthRequest` and `GoogleAuthClient`.

## 3. Code Templates

### Refresh Token Validation
```csharp
UserRefreshToken? refreshToken = await krafterContext.UserRefreshTokens
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
KrafterRole? basic = await roleManager.FindByNameAsync(KrafterRoleConstant.Basic);
if (basic is null)
{
    return Response<TokenResponse>.NotFound("Basic Role Not Found.");
}
```

## 4. Checklist
1. Use `KrafterRoute.Tokens` for token endpoints.
2. Use `RouteSegment.Refresh` for refresh.
3. Use `KrafterRoute.ExternalAuth` + `RouteSegment.Google` for Google login.
4. Pull IP from `X-Forwarded-For` when available.

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

---
Last Updated: 2026-01-25
Verified Against: Features/Auth/Login.cs, Features/Auth/RefreshToken.cs, Features/Auth/ExternalLogin.cs, Features/Auth/Common/TokenService.cs, Features/Auth/Common/UserRefreshToken.cs
---


