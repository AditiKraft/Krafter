using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web.Configuration;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OtpNet;
using QRCoder;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public sealed class TwoFactorService(
    ApplicationDbContext db, UserManager<ApplicationUser> users, ITokenService tokens,
    ITenantGetterService tenant, [FromKeyedServices(TwoFactorProtectionOptions.ProviderKey)] IDataProtectionProvider protection, TimeProvider clock,
    IOptions<SecuritySettings> security, IOptions<TwoFactorProtectionOptions> protectionOptions, ILogger<TwoFactorService> logger,
    SessionValidator sessions)
{
    private const int MaxAttempts = 5;
    // Persisted login challenges encode primary verification time as ExpiresAt minus this fixed lifetime.
    private static readonly TimeSpan LoginChallengeLifetime = TimeSpan.FromMinutes(5);
    private DateTimeOffset Now => clock.GetUtcNow();
    private string TenantId => tenant.Tenant.Id ?? throw new InvalidOperationException("Tenant context is required.");

    private Task<T> LockedAsync<T>(string userId, Func<Task<T>> action, CancellationToken cancellationToken) =>
        AccountAuthenticationTransaction.RunAsync(db, TenantId, userId, action, cancellationToken);

    internal async Task<bool> EligibleAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        user.IsActive && !user.IsDeleted && user.TenantId == TenantId && !await IsLockedOutAsync(user, cancellationToken) &&
        (!security.Value.RequireConfirmedAccount || user.EmailConfirmed);

    private async Task<bool> IsLockedOutAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool lockedOut = await users.IsLockedOutAsync(user);
        cancellationToken.ThrowIfCancellationRequested();
        return lockedOut;
    }

    private async Task<bool> UpdateFailureCountAsync(ApplicationUser user, CancellationToken cancellationToken, bool reset = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IdentityResult result = reset
            ? await users.ResetAccessFailedCountAsync(user)
            : await users.AccessFailedAsync(user);
        // Identity can reject a concurrent account change. Do not retry its stale
        // tracked user in the enclosing MFA transaction's final SaveChanges.
        if (!result.Succeeded)
        {
            db.Entry(user).State = EntityState.Detached;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return result.Succeeded;
    }

    public async Task<Response<LoginResponse>> PasswordLoginAsync(TokenRequest request, string ip, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password) || request.Password.Length > PasswordLimits.MaximumLength)
        {
            return Response<LoginResponse>.Unauthorized("Invalid email or password. If the problem persists, try again later.");
        }

        ApplicationUser? found = await users.FindByEmailAsync(request.Email.Trim().Normalize());
        if (found is null)
        {
            return Response<LoginResponse>.Unauthorized("Invalid email or password. If the problem persists, try again later.");
        }

        return await LockedAsync(found.Id, async () =>
        {
            ApplicationUser? user = await users.FindByIdAsync(found.Id);
            if (user is null || !await EligibleAsync(user, cancellationToken))
            {
                return Response<LoginResponse>.Unauthorized("Invalid email or password. If the problem persists, try again later.");
            }

            if (!await users.CheckPasswordAsync(user, request.Password))
            {
                await UpdateFailureCountAsync(user, cancellationToken);
                return Response<LoginResponse>.Unauthorized("Invalid email or password. If the problem persists, try again later.");
            }
            return await StartLoginAsync(user, ip, "pwd", Now, cancellationToken);
        }, cancellationToken);
    }

    public Task<Response<LoginResponse>> ExternalLoginAsync(string userId, string ip, CancellationToken cancellationToken,
        long authenticatedAt, string verifiedEmail, int expectedVersion) => LockedAsync(userId, async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ApplicationUser? user = await users.FindByIdAsync(userId);
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = Now;
        if (user is null || !await EligibleAsync(user, cancellationToken) ||
            string.IsNullOrWhiteSpace(verifiedEmail) || user.NormalizedEmail != users.NormalizeEmail(verifiedEmail) ||
            user.AuthenticationVersion != expectedVersion || authenticatedAt > now.ToUnixTimeSeconds() ||
            authenticatedAt <= (now - LoginChallengeLifetime).ToUnixTimeSeconds())
        {
            return Response<LoginResponse>.Unauthorized("Unable to sign in.");
        }

        DateTimeOffset primaryTime = DateTimeOffset.FromUnixTimeSeconds(authenticatedAt);
        return await StartLoginAsync(user, ip, "google", primaryTime, cancellationToken);
    }, cancellationToken);

    private async Task<Response<LoginResponse>> StartLoginAsync(ApplicationUser user, string ip, string method,
        DateTimeOffset primaryTime, CancellationToken cancellationToken)
    {
        if (!user.TwoFactorEnabled)
        {
            if (!await UpdateFailureCountAsync(user, cancellationToken, reset: true))
            {
                return Response<LoginResponse>.Unauthorized("Unable to sign in. Please try again.");
            }

            return Response<LoginResponse>.Success(LoginResponse.Completed(
                await tokens.GenerateTokensAndUpdateUser(user, ip, primaryTime.ToUnixTimeSeconds(), method,
                    googleVerifiedEmail: method == "google" ? user.Email : null, cancellationToken: cancellationToken)));
        }
        // Only one pending primary-login challenge per account; retries do not reset account failures.
        foreach (TwoFactorChallenge old in await db.TwoFactorChallenges.Where(x => x.UserId == user.Id && x.Purpose == "login").ToListAsync(cancellationToken))
        {
            old.IsDeleted = true;
        }

        (string id, TwoFactorChallenge challenge) = NewChallenge(user, "login", primaryTime + LoginChallengeLifetime);
        challenge.PrimaryMethod = method;
        await db.SaveChangesAsync(cancellationToken);
        return Response<LoginResponse>.Success(LoginResponse.Pending(id, challenge.ExpiresAt));
    }

    public async Task<Response<TokenResponse>> CompleteLoginAsync(TwoFactorRequest request, string ip, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string hash = Hash(request.ChallengeId);
        string? userId = await db.TwoFactorChallenges.Where(x => x.Id == hash && x.Purpose == "login")
            .Select(x => x.UserId).SingleOrDefaultAsync(cancellationToken);
        if (userId is null)
        {
            return Response<TokenResponse>.Unauthorized("Invalid or expired verification request.");
        }

        return await LockedAsync(userId, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplicationUser? user = await users.FindByIdAsync(userId);
            cancellationToken.ThrowIfCancellationRequested();
            TwoFactorChallenge? challenge = await ValidChallengeAsync(hash, user, "login", cancellationToken);
            if (user is null || challenge is null || !user.TwoFactorEnabled || !await EligibleAsync(user, cancellationToken))
            {
                return Response<TokenResponse>.Unauthorized("Invalid or expired verification request.");
            }

            if (!await ConsumeFactorAsync(user, request.Code, request.UseRecoveryCode, cancellationToken))
            {
                challenge.Attempts++;
                if (challenge.Attempts >= MaxAttempts)
                {
                    challenge.IsDeleted = true;
                }

                await UpdateFailureCountAsync(user, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                return Response<TokenResponse>.BadRequest("Invalid or already used code. Wait for a new code, or use a recovery code.");
            }
            challenge.IsDeleted = true;
            if (!await UpdateFailureCountAsync(user, cancellationToken, reset: true))
            {
                return Response<TokenResponse>.Unauthorized("Unable to sign in. Please try again.");
            }

            long authenticatedAt = (challenge.ExpiresAt - LoginChallengeLifetime).ToUnixTimeSeconds();
            return Response<TokenResponse>.Success(await tokens.GenerateTokensAndUpdateUser(user, ip,
                authenticatedAt, challenge.PrimaryMethod ?? "pwd", mfaVerified: true, cancellationToken: cancellationToken));
        }, cancellationToken);
    }

    public async Task<Response<TwoFactorStatusResponse>> StatusAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ApplicationUser? user = await users.FindByIdAsync(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (user is null)
        {
            return Response<TwoFactorStatusResponse>.Unauthorized("Unable to load account.");
        }

        return Response<TwoFactorStatusResponse>.Success(new(user.TwoFactorEnabled,
            await db.TwoFactorRecoveryCodes.CountAsync(x => x.UserId == userId, cancellationToken), await users.HasPasswordAsync(user)));
    }

    internal sealed record EnrollmentEmail(TwoFactorChallengeResponse Challenge, string Address, string Code);

    internal Task<Response<EnrollmentEmail>> PrepareEnrollmentEmailAsync(string userId, ClaimsPrincipal principal,
        CancellationToken cancellationToken) => LockedAsync(userId, async () =>
    {
        if (!protectionOptions.Value.EnrollmentAllowed)
        {
            return Response<EnrollmentEmail>.CustomError("Secure authenticator storage is unavailable.", 503);
        }

        ApplicationUser? user = await users.FindByIdAsync(userId);
        if (user is null || !await ValidateManagementSessionAsync(user, principal, cancellationToken) ||
            user.TwoFactorEnabled || !TryGetGoogleEnrollmentDeadline(user, principal, out DateTimeOffset signInDeadline) ||
            await users.HasPasswordAsync(user))
        {
            return Response<EnrollmentEmail>.Forbidden("Sign in with Google again using the account's verified email, then request a code within five minutes.");
        }

        const string purpose = "enroll-email";
        DateTimeOffset latestAllowedExpiry = Now.AddMinutes(4);
        // Include consumed challenges so redeeming a code cannot reset the resend limit.
        if (await db.TwoFactorChallenges.IgnoreQueryFilters().AnyAsync(x => x.TenantId == TenantId &&
            x.UserId == userId && x.Purpose == purpose && x.ExpiresAt > latestAllowedExpiry, cancellationToken))
        {
            return Response<EnrollmentEmail>.CustomError("Wait one minute before requesting another code.", 429);
        }

        foreach (TwoFactorChallenge previous in await db.TwoFactorChallenges
            .Where(x => x.UserId == userId && x.Purpose == purpose).ToListAsync(cancellationToken))
        {
            previous.IsDeleted = true;
        }
        (string id, TwoFactorChallenge challenge) = NewChallenge(user, purpose, Now.AddMinutes(5));
        string code = RandomNumberGenerator.GetInt32(100_000_000).ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
        challenge.ProtectedKey = Convert.ToHexString(EnrollmentEmailDigest(id, user, principal, code));
        await db.SaveChangesAsync(cancellationToken);
        // Keep the stored five-minute lifetime for the account resend limit across sign-ins.
        DateTimeOffset deadline = signInDeadline < challenge.ExpiresAt ? signInDeadline : challenge.ExpiresAt;
        return Response<EnrollmentEmail>.Success(new(new(id, deadline), user.Email!, code));
    }, cancellationToken);

    private bool TryGetGoogleEnrollmentDeadline(ApplicationUser user, ClaimsPrincipal principal, out DateTimeOffset deadline)
    {
        deadline = default;
        DateTimeOffset now = Now;
        if (principal.FindFirstValue(AppClaimTypes.AuthenticationMethod) != "google" ||
            !long.TryParse(principal.FindFirstValue(AppClaimTypes.AuthenticationTime), out long primaryTime) ||
            primaryTime > now.ToUnixTimeSeconds() || primaryTime <= now.AddMinutes(-5).ToUnixTimeSeconds() ||
            string.IsNullOrEmpty(principal.FindFirstValue(AppClaimTypes.SessionId)) ||
            principal.FindFirstValue(AppClaimTypes.GoogleVerifiedEmail) is not { Length: > 0 } verified ||
            string.IsNullOrWhiteSpace(user.Email) || user.NormalizedEmail != users.NormalizeEmail(verified))
        {
            return false;
        }

        deadline = DateTimeOffset.FromUnixTimeSeconds(primaryTime).AddMinutes(5);
        return true;
    }

    // Only the initiating browser holds the random handle. Its hash is the row ID, so a database
    // copy alone cannot test guesses for the short emailed code. Refresh preserves the original session identifier.
    private static byte[] EnrollmentEmailDigest(string handle, ApplicationUser user, ClaimsPrincipal principal, string code) =>
        HMACSHA256.HashData(Convert.FromHexString(handle), Encoding.UTF8.GetBytes(string.Join('\n',
            user.NormalizedEmail, user.SecurityStamp, principal.FindFirstValue(AppClaimTypes.SessionId), code)));

    private async Task<bool> ConsumeEnrollmentEmailAsync(ApplicationUser user, TwoFactorManagementRequest request,
        ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!TryGetGoogleEnrollmentDeadline(user, principal, out _) || request.EnrollmentEmailId is not { Length: 64 } id || !id.All(char.IsAsciiHexDigit))
        {
            return false;
        }

        TwoFactorChallenge? challenge = await ValidChallengeAsync(Hash(id), user, "enroll-email", cancellationToken);
        if (challenge?.ProtectedKey is null) return false;

        bool accepted = CryptographicOperations.FixedTimeEquals(Convert.FromHexString(challenge.ProtectedKey),
            EnrollmentEmailDigest(id, user, principal, request.Code?.Trim() ?? string.Empty));
        if (!accepted)
        {
            challenge.Attempts++;
            if (challenge.Attempts >= MaxAttempts) challenge.IsDeleted = true;
            await UpdateFailureCountAsync(user, cancellationToken);
        }
        else
        {
            challenge.IsDeleted = true;
            challenge.ProtectedKey = null;
        }
        await db.SaveChangesAsync(cancellationToken);
        return accepted;
    }

    public Task<Response<TwoFactorSetupResponse>> BeginEnrollmentAsync(string userId, TwoFactorManagementRequest request,
        ClaimsPrincipal principal, CancellationToken cancellationToken = default) => LockedAsync(userId, async () =>
    {
        if (!protectionOptions.Value.EnrollmentAllowed)
        {
            return Response<TwoFactorSetupResponse>.CustomError("Authenticator setup is unavailable until secure key storage is configured.", 503);
        }

        cancellationToken.ThrowIfCancellationRequested();
        ApplicationUser? user = await users.FindByIdAsync(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (user is null || !await ValidateManagementSessionAsync(user, principal, cancellationToken) ||
            !await ManagementProofAsync(user, request, principal, cancellationToken))
        {
            return Response<TwoFactorSetupResponse>.Unauthorized("Verify your password or enrolled factor. For a Google-only account, sign in again and enter the emailed code within five minutes.");
        }

        foreach (TwoFactorChallenge old in await db.TwoFactorChallenges.Where(x => x.UserId == userId && x.Purpose == "enroll").ToListAsync(cancellationToken))
        {
            old.IsDeleted = true;
        }

        string secret = Base32Encoding.ToString(RandomNumberGenerator.GetBytes(20));
        (string id, TwoFactorChallenge challenge) = NewChallenge(user, "enroll", Now.AddMinutes(5));
        challenge.ProtectedKey = Protector(userId).Protect(secret);
        string issuer = "AditiKraft.Krafter";
        string label = Uri.EscapeDataString($"{issuer}:{tenant.Tenant.Identifier ?? TenantId} ({user.Email})");
        string uri = $"otpauth://totp/{label}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits=6&period=30&algorithm=SHA1";
        using QRCodeData data = QRCodeGenerator.GenerateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        using var svg = new SvgQRCode(data);
        await db.SaveChangesAsync(cancellationToken);
        return Response<TwoFactorSetupResponse>.Success(new(id, secret, svg.GetGraphic(4), challenge.ExpiresAt));
    }, cancellationToken);

    public Task<Response<RecoveryCodesResponse>> ConfirmEnrollmentAsync(string userId, TwoFactorRequest request,
        ClaimsPrincipal principal, CancellationToken cancellationToken = default) => LockedAsync(userId, async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ApplicationUser? user = await users.FindByIdAsync(userId);
        cancellationToken.ThrowIfCancellationRequested();
        TwoFactorChallenge? challenge = await ValidChallengeAsync(Hash(request.ChallengeId), user, "enroll", cancellationToken);
        if (user is null || challenge?.ProtectedKey is null || !await ValidateManagementSessionAsync(user, principal, cancellationToken))
        {
            return Response<RecoveryCodesResponse>.Unauthorized("Setup expired. Begin setup again.");
        }

        string secret = Protector(userId).Unprotect(challenge.ProtectedKey);
        if (request.UseRecoveryCode || !VerifyTotp(secret, request.Code, out long step))
        {
            challenge.Attempts++;
            if (challenge.Attempts >= MaxAttempts)
            {
                challenge.IsDeleted = true;
            }

            await UpdateFailureCountAsync(user, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return Response<RecoveryCodesResponse>.BadRequest("Invalid code. Check your authenticator and try again.");
        }
        TwoFactorCredential? credential = await db.TwoFactorCredentials.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (credential is null)
        {
            credential = new TwoFactorCredential { UserId = userId, TenantId = TenantId };
            db.TwoFactorCredentials.Add(credential);
        }
        credential.ProtectedKey = challenge.ProtectedKey;
        credential.LastAcceptedStep = step;
        user.TwoFactorEnabled = true;
        await RevokeSessionsAsync(user, cancellationToken);
        IReadOnlyList<string> codes = await ReplaceRecoveryCodesAsync(user, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Response<RecoveryCodesResponse>.Success(new(codes));
    }, cancellationToken);

    public Task<Response> DisableAsync(string userId, TwoFactorManagementRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken = default) => LockedAsync(userId, async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ApplicationUser? user = await users.FindByIdAsync(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (user is null || !user.TwoFactorEnabled || !await ValidateManagementSessionAsync(user, principal, cancellationToken) ||
            !await ManagementProofAsync(user, request, principal, cancellationToken))
        {
            return Response.Unauthorized("A valid authenticator or recovery code is required.");
        }

        user.TwoFactorEnabled = false;
        TwoFactorCredential? credential = await db.TwoFactorCredentials.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (credential is not null)
        {
            credential.ProtectedKey = null;
            credential.LastAcceptedStep = -1;
        }
        foreach (TwoFactorRecoveryCode code in await db.TwoFactorRecoveryCodes.Where(x => x.UserId == userId).ToListAsync(cancellationToken))
        {
            code.IsDeleted = true;
        }

        await RevokeSessionsAsync(user, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Response.Success("Two-factor authentication disabled. Sign in again.");
    }, cancellationToken);

    public Task<Response<RecoveryCodesResponse>> RegenerateAsync(string userId, TwoFactorManagementRequest request,
        ClaimsPrincipal principal, CancellationToken cancellationToken = default) => LockedAsync(userId, async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ApplicationUser? user = await users.FindByIdAsync(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (user is null || !user.TwoFactorEnabled || !await ValidateManagementSessionAsync(user, principal, cancellationToken) ||
            !await ManagementProofAsync(user, request, principal, cancellationToken))
        {
            return Response<RecoveryCodesResponse>.Unauthorized("A valid authenticator or recovery code is required.");
        }

        IReadOnlyList<string> codes = await ReplaceRecoveryCodesAsync(user, cancellationToken);
        await RevokeSessionsAsync(user, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Response<RecoveryCodesResponse>.Success(new(codes));
    }, cancellationToken);

    private async Task<bool> ValidateManagementSessionAsync(ApplicationUser user, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        // Middleware may have authenticated this request before another transition revoked it.
        // Recheck its original claims against the state reloaded under the account lock.
        return principal.Identity?.IsAuthenticated == true &&
            principal.FindFirstValue(ClaimTypes.NameIdentifier) == user.Id &&
            await sessions.ValidateAsync(principal, cancellationToken);
    }

    private async Task<bool> ManagementProofAsync(ApplicationUser user, TwoFactorManagementRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (user.TwoFactorEnabled)
        {
            if (await ConsumeFactorAsync(user, request.Code, request.UseRecoveryCode, cancellationToken))
            {
                return true;
            }

            await UpdateFailureCountAsync(user, cancellationToken);
            return false;
        }
        if (await users.HasPasswordAsync(user))
        {
            if (!string.IsNullOrEmpty(request.Password) && await users.CheckPasswordAsync(user, request.Password))
            {
                return true;
            }

            await UpdateFailureCountAsync(user, cancellationToken);
            return false;
        }
        return await ConsumeEnrollmentEmailAsync(user, request, principal, cancellationToken);
    }

    private async Task<bool> ConsumeFactorAsync(ApplicationUser user, string code, bool recovery, CancellationToken cancellationToken)
    {
        if (recovery)
        {
            string hash = Hash(Normalize(code).ToUpperInvariant());
            TwoFactorRecoveryCode? entry = await db.TwoFactorRecoveryCodes.SingleOrDefaultAsync(x => x.UserId == user.Id && x.CodeHash == hash, cancellationToken);
            if (entry is null)
            {
                return false;
            }

            entry.IsDeleted = true;
            return true;
        }
        TwoFactorCredential? credential = await db.TwoFactorCredentials.SingleOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
        if (credential?.ProtectedKey is null)
        {
            return false;
        }

        string secret = Protector(user.Id).Unprotect(credential.ProtectedKey);
        if (!VerifyTotp(secret, code, out long step) || step <= credential.LastAcceptedStep)
        {
            return false;
        }

        credential.LastAcceptedStep = step;
        return true;
    }

    private bool VerifyTotp(string secret, string input, out long step)
    {
        string code = Normalize(input);
        step = -1;
        return code.Length == 6 && code.All(c => c is >= '0' and <= '9') &&
            new Totp(Base32Encoding.ToBytes(secret)).VerifyTotp(Now.UtcDateTime, code, out step, new VerificationWindow(previous: 1, future: 1));
    }

    private async Task<TwoFactorChallenge?> ValidChallengeAsync(string hash, ApplicationUser? user, string purpose, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            return null;
        }

        return await db.TwoFactorChallenges.SingleOrDefaultAsync(x => x.Id == hash && x.UserId == user.Id &&
            x.Purpose == purpose && x.AuthenticationVersion == user.AuthenticationVersion && x.Attempts < MaxAttempts && x.ExpiresAt > Now, cancellationToken);
    }

    private (string Id, TwoFactorChallenge Challenge) NewChallenge(ApplicationUser user, string purpose, DateTimeOffset expiresAt)
    {
        string id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var challenge = new TwoFactorChallenge
        {
            Id = Hash(id),
            UserId = user.Id,
            TenantId = TenantId,
            Purpose = purpose,
            AuthenticationVersion = user.AuthenticationVersion,
            ExpiresAt = expiresAt
        };
        db.TwoFactorChallenges.Add(challenge);
        return (id, challenge);
    }

    private async Task<IReadOnlyList<string>> ReplaceRecoveryCodesAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        foreach (TwoFactorRecoveryCode old in await db.TwoFactorRecoveryCodes.Where(x => x.UserId == user.Id).ToListAsync(cancellationToken))
        {
            old.IsDeleted = true;
        }

        var codes = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            string raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            codes.Add(string.Join('-', Enumerable.Range(0, 8).Select(j => raw.Substring(j * 4, 4))));
            db.TwoFactorRecoveryCodes.Add(new TwoFactorRecoveryCode
            {
                Id = Guid.NewGuid().ToString(),
                UserId = user.Id,
                TenantId = TenantId,
                CodeHash = Hash(raw)
            });
        }
        return codes;
    }

    private async Task RevokeSessionsAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.AuthenticationVersion++;
        logger.LogInformation("Account security changed; previous sessions revoked. Tenant {TenantId}, user {UserId}, version {Version}.",
            TenantId, user.Id, user.AuthenticationVersion);
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        user.AccessFailedCount = 0;
        foreach (TwoFactorChallenge c in await db.TwoFactorChallenges.Where(x => x.UserId == user.Id).ToListAsync(cancellationToken))
        {
            c.IsDeleted = true;
            c.ProtectedKey = null;
        }
        UserRefreshToken? refresh = await db.UserRefreshTokens.SingleOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
        if (refresh is not null)
        {
            refresh.RefreshToken = null;
            refresh.RefreshTokenExpiryTime = Now.UtcDateTime;
        }
    }

    private IDataProtector Protector(string userId) => protection.CreateProtector("Krafter.Totp.v1", TenantId, userId);
    private static string Normalize(string? code) => (code ?? string.Empty).Replace(" ", "").Replace("-", "").Trim();
    private static string Hash(string? value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)));
}
