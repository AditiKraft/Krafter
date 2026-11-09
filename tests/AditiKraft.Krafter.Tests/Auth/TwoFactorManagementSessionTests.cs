using System.Security.Claims;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Tests.Auth;

[Collection("MFA PostgreSQL")]
public sealed class TwoFactorManagementSessionTests
{
    [Theory]
    [InlineData("setup", false)]
    [InlineData("confirm", false)]
    [InlineData("disable", false)]
    [InlineData("regenerate", false)]
    [InlineData("setup", true)]
    [InlineData("confirm", true)]
    [InlineData("disable", true)]
    [InlineData("regenerate", true)]
    public async Task ManagementRejectsRevokedVersionsAndMissingMfaWithoutConsumingProofAsync(string operation, bool missingMfa)
    {
        using var harness = new MfaHarness();
        using MfaScope setup = harness.Scope();
        ApplicationUser user = await setup.AddUserAsync();
        (_, IReadOnlyList<string> codes) = await harness.EnrollAsync(setup, user);
        ClaimsPrincipal original = await LoginAsync(setup, user.Id, codes[0]);
        ClaimsPrincipal current = original;
        ClaimsPrincipal rejected;
        if (missingMfa)
        {
            rejected = new ClaimsPrincipal(new ClaimsIdentity(original.Claims
                .Where(claim => claim.Type != AppClaimTypes.Mfa), "test"));
        }
        else
        {
            // A successful password reset revokes the original session while retaining its factor.
            var reset = await new ResetPassword.Handler(setup.Users, setup.Db).ResetPasswordAsync(new()
            {
                Email = user.Email,
                Token = await setup.Users.GeneratePasswordResetTokenAsync(user),
                Password = MfaHarness.Password
            });
            Assert.False(reset.IsError);
            current = await LoginAsync(setup, user.Id, codes[1]);
            rejected = original;
        }

        TwoFactorRequest? confirmation = null;
        if (operation == "confirm")
        {
            var enrollment = await setup.Mfa.BeginEnrollmentAsync(user.Id,
                new() { Code = codes[2], UseRecoveryCode = true }, current);
            Assert.False(enrollment.IsError);
            confirmation = new TwoFactorRequest
            {
                ChallengeId = enrollment.Data!.EnrollmentId,
                Code = harness.Code(enrollment.Data.SharedKey)
            };
        }

        int version = (await setup.Db.Users.AsNoTracking().SingleAsync()).AuthenticationVersion;
        string? protectedKey = (await setup.Db.TwoFactorCredentials.AsNoTracking().SingleAsync()).ProtectedKey;
        int recoveryCount = await setup.Db.TwoFactorRecoveryCodes.CountAsync();
        string[] challenges = await setup.Db.TwoFactorChallenges.OrderBy(challenge => challenge.Id)
            .Select(challenge => challenge.Id).ToArrayAsync();
        using MfaScope operationScope = harness.Scope();
        Assert.Equal(401, await ManageAsync(operationScope, operation, user.Id, rejected, codes[3], confirmation));

        using MfaScope check = harness.Scope();
        ApplicationUser saved = (await check.Users.FindByIdAsync(user.Id))!;
        Assert.True(saved.TwoFactorEnabled);
        Assert.Equal(version, saved.AuthenticationVersion);
        Assert.Equal(0, saved.AccessFailedCount);
        Assert.Equal(protectedKey, (await check.Db.TwoFactorCredentials.SingleAsync()).ProtectedKey);
        Assert.Equal(recoveryCount, await check.Db.TwoFactorRecoveryCodes.CountAsync());
        Assert.Equal(challenges, await check.Db.TwoFactorChallenges.OrderBy(challenge => challenge.Id)
            .Select(challenge => challenge.Id).ToArrayAsync());
        Assert.All(await check.Db.TwoFactorChallenges.ToListAsync(), challenge => Assert.Equal(0, challenge.Attempts));
        // The current session can use the identical proof; rejection must not burn it.
        Assert.Equal(200, await ManageAsync(check, operation, user.Id, current, codes[3], confirmation));
    }

    [PostgresMfaFact]
    public Task PreviouslyAuthenticatedGoogleSetupCannotSurviveMfaDisableAsync() =>
        VerifyQueuedGoogleManagementAsync(confirm: false);

    [PostgresMfaFact]
    public Task PreviouslyAuthenticatedConfirmationCannotConsumeANewEnrollmentAsync() =>
        VerifyQueuedGoogleManagementAsync(confirm: true);

    private static async Task VerifyQueuedGoogleManagementAsync(bool confirm)
    {
        using var harness = new MfaHarness(Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!);
        string tenantId = Guid.NewGuid().ToString();
        using MfaScope setup = harness.Scope(tenantId);
        await setup.Db.Database.MigrateAsync();
        ApplicationUser user = await setup.AddUserAsync();
        (_, IReadOnlyList<string> codes) = await harness.EnrollAsync(setup, user);
        user = (await setup.Users.FindByIdAsync(user.Id))!;
        Assert.True((await setup.Users.RemovePasswordAsync(user)).Succeeded);
        ClaimsPrincipal original = await LoginAsync(setup, user.Id, codes[0], google: true);
        using MfaScope waiting = harness.Scope(tenantId);
        // HTTP middleware accepted this principal before endpoint body binding/lock acquisition.
        // Also leave its old account tracked so validation must occur after the lock's reload.
        Assert.True(await waiting.Sessions.ValidateAsync(original));

        using MfaScope changing = harness.Scope(tenantId);
        Assert.False((await changing.Mfa.DisableAsync(user.Id,
            new() { Code = codes[1], UseRecoveryCode = true }, original)).IsError);
        int disabledVersion = (await changing.Db.Users.AsNoTracking().SingleAsync(x => x.Id == user.Id)).AuthenticationVersion;

        using MfaScope fresh = harness.Scope(tenantId);
        var login = await fresh.Mfa.ExternalLoginAsync(user.Id, "test");
        Assert.False(login.IsError);
        ClaimsPrincipal current = MfaHarness.Principal(login.Data!.Tokens!);
        TwoFactorSetupResponse? enrollment = null;
        if (confirm)
        {
            var begun = await fresh.Mfa.BeginEnrollmentAsync(user.Id, new(), current);
            Assert.False(begun.IsError);
            enrollment = begun.Data!;
            Assert.Equal(401, (await waiting.Mfa.ConfirmEnrollmentAsync(user.Id,
                new() { ChallengeId = enrollment.EnrollmentId, Code = harness.Code(enrollment.SharedKey) }, original)).StatusCode);
        }
        else
        {
            Assert.Equal(401, (await waiting.Mfa.BeginEnrollmentAsync(user.Id, new(), original)).StatusCode);
        }

        using MfaScope check = harness.Scope(tenantId);
        ApplicationUser saved = (await check.Users.FindByIdAsync(user.Id))!;
        Assert.False(saved.TwoFactorEnabled);
        Assert.Equal(disabledVersion, saved.AuthenticationVersion);
        Assert.Equal(0, saved.AccessFailedCount);
        Assert.Null((await check.Db.TwoFactorCredentials.SingleAsync(x => x.UserId == user.Id)).ProtectedKey);
        Assert.Equal(0, await check.Db.TwoFactorRecoveryCodes.CountAsync(x => x.UserId == user.Id));
        Assert.Equal(confirm ? 1 : 0, await check.Db.TwoFactorChallenges.CountAsync(x => x.UserId == user.Id));
        Assert.All(await check.Db.TwoFactorChallenges.Where(x => x.UserId == user.Id).ToListAsync(),
            challenge => Assert.Equal(0, challenge.Attempts));

        if (enrollment is null)
        {
            var begun = await check.Mfa.BeginEnrollmentAsync(user.Id, new(), current);
            Assert.False(begun.IsError);
            enrollment = begun.Data!;
        }
        // A fresh Google session can still enroll one authenticator and receive its recovery batch.
        var completed = await check.Mfa.ConfirmEnrollmentAsync(user.Id,
            new() { ChallengeId = enrollment.EnrollmentId, Code = harness.Code(enrollment.SharedKey) }, current);
        Assert.False(completed.IsError);
        Assert.Equal(10, completed.Data!.Codes.Count);
        Assert.Equal(1, await check.Db.TwoFactorCredentials.CountAsync(x => x.UserId == user.Id));
        Assert.Equal(disabledVersion + 1, (await check.Db.Users.AsNoTracking().SingleAsync(x => x.Id == user.Id)).AuthenticationVersion);
    }

    private static async Task<ClaimsPrincipal> LoginAsync(MfaScope scope, string userId, string code, bool google = false)
    {
        var primary = google
            ? await scope.Mfa.ExternalLoginAsync(userId, "test")
            : await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        Assert.False(primary.IsError);
        var completed = await scope.Mfa.CompleteLoginAsync(new()
        { ChallengeId = primary.Data!.Challenge!.Id, Code = code, UseRecoveryCode = true }, "test");
        Assert.False(completed.IsError);
        return MfaHarness.Principal(completed.Data!);
    }

    private static async Task<int> ManageAsync(MfaScope scope, string operation, string userId,
        ClaimsPrincipal principal, string recoveryCode, TwoFactorRequest? confirmation)
    {
        var proof = new TwoFactorManagementRequest { Code = recoveryCode, UseRecoveryCode = true };
        return operation switch
        {
            "setup" => (await scope.Mfa.BeginEnrollmentAsync(userId, proof, principal)).StatusCode,
            "confirm" => (await scope.Mfa.ConfirmEnrollmentAsync(userId, confirmation!, principal)).StatusCode,
            "disable" => (await scope.Mfa.DisableAsync(userId, proof, principal)).StatusCode,
            "regenerate" => (await scope.Mfa.RegenerateAsync(userId, proof, principal)).StatusCode,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }
}
