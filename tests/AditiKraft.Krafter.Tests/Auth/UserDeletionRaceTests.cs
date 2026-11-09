using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class UserDeletionRaceTests
{
    [Theory]
    [InlineData("wrong-password")]
    [InlineData("correct-password")]
    [InlineData("wrong-second-factor")]
    public Task InFlightAuthenticationCannotRestoreADeletedAccount(string flow) => VerifyAsync(null, flow);

    internal static async Task VerifyAsync(string? postgres, string flow)
    {
        var interloper = new BeforeSaveInterloper();
        using var harness = new MfaHarness(postgres, interceptor: interloper);
        string tenant = Guid.NewGuid().ToString();
        using MfaScope seed = harness.Scope(tenant);
        if (seed.Db.Database.IsRelational()) await seed.Db.Database.MigrateAsync();
        var user = await seed.AddUserAsync();
        var login = await seed.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        var tokens = login.Data!.Tokens!;
        string? challengeId = null;
        if (flow == "wrong-second-factor")
        {
            var enrollment = await harness.EnrollAsync(seed, user);
            var primary = await seed.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
            var verified = await seed.Mfa.CompleteLoginAsync(new()
            { ChallengeId = primary.Data!.Challenge!.Id, Code = enrollment.Codes[0], UseRecoveryCode = true }, "test");
            Assert.False(verified.IsError);
            tokens = verified.Data!;
            challengeId = (await seed.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test")).Data!.Challenge!.Id;
        }
        else if (flow == "correct-password")
        {
            // Successful password login must save the reset failure count too.
            Assert.True((await seed.Users.AccessFailedAsync(user)).Succeeded);
        }
        int originalVersion = user.AuthenticationVersion;
        var principal = MfaHarness.Principal(tokens);
        Assert.True(await seed.Sessions.ValidateAsync(principal));
        bool deletionObserved = false;
        interloper.BeforeNextSave = async () =>
        {
            using MfaScope deleting = harness.Scope(tenant);
            Assert.False((await new DeleteUser.Handler(deleting.Users, deleting.Db).DeleteAsync(user.Id)).IsError);
            using MfaScope observed = harness.Scope(tenant);
            Assert.True((await observed.Db.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == user.Id)).IsDeleted);
            Assert.False(await observed.Sessions.ValidateAsync(principal));
            deletionObserved = true;
        };
        using MfaScope authenticating = harness.Scope(tenant);
        if (flow == "wrong-second-factor")
        {
            var rejected = await authenticating.Mfa.CompleteLoginAsync(new()
            { ChallengeId = challengeId!, Code = "invalid-factor" }, "test");
            Assert.True(rejected.IsError);
        }
        else
        {
            TokenRequest request = MfaHarness.PasswordRequest();
            if (flow == "wrong-password") request.Password = "incorrect-password";
            Assert.True((await authenticating.Mfa.PasswordLoginAsync(request, "test")).IsError);
        }
        Assert.True(deletionObserved);
        using MfaScope fresh = harness.Scope(tenant);
        var saved = await fresh.Db.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == user.Id);
        Assert.True(saved.IsDeleted);
        Assert.Equal(originalVersion + 1, saved.AuthenticationVersion);
        Assert.False(await fresh.Sessions.ValidateAsync(principal));
        Assert.Null(await fresh.Users.FindByIdAsync(user.Id));
    }

    private sealed class BeforeSaveInterloper : SaveChangesInterceptor
    {
        public Func<Task>? BeforeNextSave;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Func<Task>? action = BeforeNextSave;
            BeforeNextSave = null;
            if (action is not null) await action();
            return result;
        }
    }
}
