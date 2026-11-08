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
    ITenantGetterService tenant, IDataProtectionProvider protection, TimeProvider clock,
    IOptions<SecuritySettings> security, IOptions<TwoFactorProtectionOptions> protectionOptions, ILogger<TwoFactorService> logger)
{
    private const int MaxAttempts = 5;
    private DateTimeOffset Now => clock.GetUtcNow();
    private string TenantId => tenant.Tenant.Id ?? throw new InvalidOperationException("Tenant context is required.");

    // PostgreSQL is the supported production provider. Transaction-level advisory locks serialize
    // every transition for an account, including its first enrollment, across API replicas.
    private async Task<T> LockedAsync<T>(string userId, Func<Task<T>> action)
    {
        if (!db.Database.IsRelational())
            return await action(); // Unit tests only; concurrency guarantees require PostgreSQL tests.
        await using var transaction = await db.Database.BeginTransactionAsync();
        string key = $"mfa:{TenantId}:{userId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))");
        db.ChangeTracker.Clear();
        T result = await action();
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return result;
    }

    private async Task<bool> EligibleAsync(ApplicationUser user) =>
        user.IsActive && !user.IsDeleted && user.TenantId == TenantId && !await users.IsLockedOutAsync(user) &&
        (!security.Value.RequireConfirmedAccount || user.EmailConfirmed);

    public async Task<Response<LoginResponse>> PasswordLoginAsync(TokenRequest request, string ip)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024)
            return Response<LoginResponse>.Unauthorized("Invalid email or password. If the problem persists, try again later.");
        ApplicationUser? found = await users.FindByEmailAsync(request.Email.Trim().Normalize());
        if (found is null) return Response<LoginResponse>.Unauthorized("Invalid email or password. If the problem persists, try again later.");
        return await LockedAsync(found.Id, async () =>
        {
            ApplicationUser? user = await users.FindByIdAsync(found.Id);
            if (user is null || !await EligibleAsync(user))
                return Response<LoginResponse>.Unauthorized("Invalid email or password. If the problem persists, try again later.");
            if (!await users.CheckPasswordAsync(user, request.Password))
            {
                await users.AccessFailedAsync(user);
                return Response<LoginResponse>.Unauthorized("Invalid email or password. If the problem persists, try again later.");
            }
            return await StartLoginAsync(user, ip, "pwd");
        });
    }

    public Task<Response<LoginResponse>> ExternalLoginAsync(string userId, string ip) => LockedAsync(userId, async () =>
    {
        ApplicationUser? user = await users.FindByIdAsync(userId);
        if (user is null || !await EligibleAsync(user))
            return Response<LoginResponse>.Unauthorized("Unable to sign in.");
        return await StartLoginAsync(user, ip, "google");
    });

    private async Task<Response<LoginResponse>> StartLoginAsync(ApplicationUser user, string ip, string method)
    {
        if (!user.TwoFactorEnabled)
        {
            await users.ResetAccessFailedCountAsync(user);
            return Response<LoginResponse>.Success(LoginResponse.Completed(
                await tokens.GenerateTokensAndUpdateUser(user, ip, Now.ToUnixTimeSeconds(), method)));
        }
        // Only one pending primary-login challenge per account; retries do not reset account failures.
        foreach (TwoFactorChallenge old in await db.TwoFactorChallenges.Where(x => x.UserId == user.Id && x.Purpose == "login").ToListAsync())
            old.IsDeleted = true;
        (string id, TwoFactorChallenge challenge) = NewChallenge(user, "login");
        challenge.PrimaryMethod = method;
        await db.SaveChangesAsync();
        return Response<LoginResponse>.Success(LoginResponse.Pending(id, challenge.ExpiresAt));
    }

    public async Task<Response<TokenResponse>> CompleteLoginAsync(TwoFactorRequest request, string ip)
    {
        string hash = Hash(request.ChallengeId);
        string? userId = await db.TwoFactorChallenges.Where(x => x.Id == hash && x.Purpose == "login")
            .Select(x => x.UserId).SingleOrDefaultAsync();
        if (userId is null) return Response<TokenResponse>.Unauthorized("Invalid or expired verification request.");
        return await LockedAsync(userId, async () =>
        {
            ApplicationUser? user = await users.FindByIdAsync(userId);
            TwoFactorChallenge? challenge = await ValidChallengeAsync(hash, user, "login");
            if (user is null || challenge is null || !user.TwoFactorEnabled || !await EligibleAsync(user))
                return Response<TokenResponse>.Unauthorized("Invalid or expired verification request.");
            if (!await ConsumeFactorAsync(user, request.Code, request.UseRecoveryCode))
            {
                challenge.Attempts++;
                if (challenge.Attempts >= MaxAttempts) challenge.IsDeleted = true;
                await users.AccessFailedAsync(user);
                await db.SaveChangesAsync();
                return Response<TokenResponse>.BadRequest("Invalid or already used code. Wait for a new code, or use a recovery code.");
            }
            challenge.IsDeleted = true;
            await users.ResetAccessFailedCountAsync(user);
            return Response<TokenResponse>.Success(await tokens.GenerateTokensAndUpdateUser(user, ip,
                Now.ToUnixTimeSeconds(), challenge.PrimaryMethod ?? "pwd", mfaVerified: true));
        });
    }

    public async Task<Response<TwoFactorStatusResponse>> StatusAsync(string userId)
    {
        ApplicationUser? user = await users.FindByIdAsync(userId);
        if (user is null) return Response<TwoFactorStatusResponse>.Unauthorized("Unable to load account.");
        return Response<TwoFactorStatusResponse>.Success(new(user.TwoFactorEnabled,
            await db.TwoFactorRecoveryCodes.CountAsync(x => x.UserId == userId), await users.HasPasswordAsync(user)));
    }

    public Task<Response<TwoFactorSetupResponse>> BeginEnrollmentAsync(string userId, TwoFactorManagementRequest request,
        ClaimsPrincipal principal) => LockedAsync(userId, async () =>
    {
        if (!protectionOptions.Value.EnrollmentAllowed)
            return Response<TwoFactorSetupResponse>.CustomError("Authenticator setup is unavailable until secure key storage is configured.", 503);
        ApplicationUser? user = await users.FindByIdAsync(userId);
        if (user is null || !await EligibleAsync(user) || !await ManagementProofAsync(user, request, principal))
            return Response<TwoFactorSetupResponse>.Unauthorized("Verify your password or enrolled factor. Google-only accounts must sign in again within five minutes.");
        foreach (TwoFactorChallenge old in await db.TwoFactorChallenges.Where(x => x.UserId == userId && x.Purpose == "enroll").ToListAsync())
            old.IsDeleted = true;
        string secret = Base32Encoding.ToString(RandomNumberGenerator.GetBytes(20));
        (string id, TwoFactorChallenge challenge) = NewChallenge(user, "enroll");
        challenge.ProtectedKey = Protector(userId).Protect(secret);
        string issuer = "AditiKraft.Krafter";
        string label = Uri.EscapeDataString($"{issuer}:{tenant.Tenant.Identifier ?? TenantId} ({user.Email})");
        string uri = $"otpauth://totp/{label}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits=6&period=30&algorithm=SHA1";
        using QRCodeData data = QRCodeGenerator.GenerateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        using var svg = new SvgQRCode(data);
        await db.SaveChangesAsync();
        return Response<TwoFactorSetupResponse>.Success(new(id, secret, svg.GetGraphic(4), challenge.ExpiresAt));
    });

    public Task<Response<RecoveryCodesResponse>> ConfirmEnrollmentAsync(string userId, TwoFactorRequest request) => LockedAsync(userId, async () =>
    {
        ApplicationUser? user = await users.FindByIdAsync(userId);
        TwoFactorChallenge? challenge = await ValidChallengeAsync(Hash(request.ChallengeId), user, "enroll");
        if (user is null || challenge?.ProtectedKey is null || !await EligibleAsync(user))
            return Response<RecoveryCodesResponse>.Unauthorized("Setup expired. Begin setup again.");
        string secret = Protector(userId).Unprotect(challenge.ProtectedKey);
        if (request.UseRecoveryCode || !VerifyTotp(secret, request.Code, out long step))
        {
            challenge.Attempts++;
            if (challenge.Attempts >= MaxAttempts) challenge.IsDeleted = true;
            await users.AccessFailedAsync(user);
            await db.SaveChangesAsync();
            return Response<RecoveryCodesResponse>.BadRequest("Invalid code. Check your authenticator and try again.");
        }
        TwoFactorCredential? credential = await db.TwoFactorCredentials.SingleOrDefaultAsync(x => x.UserId == userId);
        if (credential is null)
        {
            credential = new TwoFactorCredential { UserId = userId, TenantId = TenantId };
            db.TwoFactorCredentials.Add(credential);
        }
        credential.ProtectedKey = challenge.ProtectedKey;
        credential.LastAcceptedStep = step;
        user.TwoFactorEnabled = true;
        await RevokeSessionsAsync(user);
        IReadOnlyList<string> codes = await ReplaceRecoveryCodesAsync(user);
        await db.SaveChangesAsync();
        return Response<RecoveryCodesResponse>.Success(new(codes));
    });

    public Task<Response> DisableAsync(string userId, TwoFactorManagementRequest request, ClaimsPrincipal principal) => LockedAsync(userId, async () =>
    {
        ApplicationUser? user = await users.FindByIdAsync(userId);
        if (user is null || !user.TwoFactorEnabled || !await EligibleAsync(user) || !await ManagementProofAsync(user, request, principal))
            return Response.Unauthorized("A valid authenticator or recovery code is required.");
        user.TwoFactorEnabled = false;
        TwoFactorCredential? credential = await db.TwoFactorCredentials.SingleOrDefaultAsync(x => x.UserId == userId);
        if (credential is not null) { credential.ProtectedKey = null; credential.LastAcceptedStep = -1; }
        foreach (TwoFactorRecoveryCode code in await db.TwoFactorRecoveryCodes.Where(x => x.UserId == userId).ToListAsync()) code.IsDeleted = true;
        await RevokeSessionsAsync(user);
        await db.SaveChangesAsync();
        return Response.Success("Two-factor authentication disabled. Sign in again.");
    });

    public Task<Response<RecoveryCodesResponse>> RegenerateAsync(string userId, TwoFactorManagementRequest request,
        ClaimsPrincipal principal) => LockedAsync(userId, async () =>
    {
        ApplicationUser? user = await users.FindByIdAsync(userId);
        if (user is null || !user.TwoFactorEnabled || !await EligibleAsync(user) || !await ManagementProofAsync(user, request, principal))
            return Response<RecoveryCodesResponse>.Unauthorized("A valid authenticator or recovery code is required.");
        IReadOnlyList<string> codes = await ReplaceRecoveryCodesAsync(user);
        await RevokeSessionsAsync(user);
        await db.SaveChangesAsync();
        return Response<RecoveryCodesResponse>.Success(new(codes));
    });

    private async Task<bool> ManagementProofAsync(ApplicationUser user, TwoFactorManagementRequest request, ClaimsPrincipal principal)
    {
        if (user.TwoFactorEnabled)
        {
            if (await ConsumeFactorAsync(user, request.Code, request.UseRecoveryCode)) return true;
            await users.AccessFailedAsync(user);
            return false;
        }
        if (await users.HasPasswordAsync(user))
        {
            if (!string.IsNullOrEmpty(request.Password) && await users.CheckPasswordAsync(user, request.Password)) return true;
            await users.AccessFailedAsync(user);
            return false;
        }
        return principal.FindFirstValue(AppClaimTypes.AuthenticationMethod) == "google" &&
            long.TryParse(principal.FindFirstValue(AppClaimTypes.AuthenticationTime), out long seconds) &&
            seconds <= Now.ToUnixTimeSeconds() && seconds >= Now.AddMinutes(-5).ToUnixTimeSeconds();
    }

    private async Task<bool> ConsumeFactorAsync(ApplicationUser user, string code, bool recovery)
    {
        if (recovery)
        {
            string hash = Hash(Normalize(code).ToUpperInvariant());
            TwoFactorRecoveryCode? entry = await db.TwoFactorRecoveryCodes.SingleOrDefaultAsync(x => x.UserId == user.Id && x.CodeHash == hash);
            if (entry is null) return false;
            entry.IsDeleted = true;
            return true;
        }
        TwoFactorCredential? credential = await db.TwoFactorCredentials.SingleOrDefaultAsync(x => x.UserId == user.Id);
        if (credential?.ProtectedKey is null) return false;
        string secret = Protector(user.Id).Unprotect(credential.ProtectedKey);
        if (!VerifyTotp(secret, code, out long step) || step <= credential.LastAcceptedStep) return false;
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

    private async Task<TwoFactorChallenge?> ValidChallengeAsync(string hash, ApplicationUser? user, string purpose)
    {
        if (user is null) return null;
        return await db.TwoFactorChallenges.SingleOrDefaultAsync(x => x.Id == hash && x.UserId == user.Id &&
            x.Purpose == purpose && x.AuthenticationVersion == user.AuthenticationVersion && x.Attempts < MaxAttempts && x.ExpiresAt > Now);
    }

    private (string Id, TwoFactorChallenge Challenge) NewChallenge(ApplicationUser user, string purpose)
    {
        string id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var challenge = new TwoFactorChallenge { Id = Hash(id), UserId = user.Id, TenantId = TenantId,
            Purpose = purpose, AuthenticationVersion = user.AuthenticationVersion, ExpiresAt = Now.AddMinutes(5) };
        db.TwoFactorChallenges.Add(challenge);
        return (id, challenge);
    }

    private async Task<IReadOnlyList<string>> ReplaceRecoveryCodesAsync(ApplicationUser user)
    {
        foreach (TwoFactorRecoveryCode old in await db.TwoFactorRecoveryCodes.Where(x => x.UserId == user.Id).ToListAsync()) old.IsDeleted = true;
        var codes = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            string raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            codes.Add(string.Join('-', Enumerable.Range(0, 8).Select(j => raw.Substring(j * 4, 4))));
            db.TwoFactorRecoveryCodes.Add(new TwoFactorRecoveryCode { Id = Guid.NewGuid().ToString(), UserId = user.Id,
                TenantId = TenantId, CodeHash = Hash(raw) });
        }
        return codes;
    }

    private async Task RevokeSessionsAsync(ApplicationUser user)
    {
        user.AuthenticationVersion++;
        logger.LogInformation("Account security changed; previous sessions revoked. Tenant {TenantId}, user {UserId}, version {Version}.",
            TenantId, user.Id, user.AuthenticationVersion);
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        user.AccessFailedCount = 0;
        foreach (TwoFactorChallenge c in await db.TwoFactorChallenges.Where(x => x.UserId == user.Id).ToListAsync())
        { c.IsDeleted = true; c.ProtectedKey = null; }
        UserRefreshToken? refresh = await db.UserRefreshTokens.SingleOrDefaultAsync(x => x.UserId == user.Id);
        if (refresh is not null) { refresh.RefreshToken = null; refresh.RefreshTokenExpiryTime = Now.UtcDateTime; }
    }

    private IDataProtector Protector(string userId) => protection.CreateProtector("Krafter.Totp.v1", TenantId, userId);
    private static string Normalize(string? code) => (code ?? string.Empty).Replace(" ", "").Replace("-", "").Trim();
    private static string Hash(string? value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)));
}
