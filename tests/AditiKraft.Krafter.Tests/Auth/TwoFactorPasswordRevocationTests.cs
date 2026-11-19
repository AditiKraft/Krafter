using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class TwoFactorPasswordRevocationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PasswordAndSessionVersionChangeTogetherDespiteAnImmediateConcurrentUpdate(bool reset)
    {
        var interceptor = new Interloper();
        using var harness = new MfaHarness(interceptor: interceptor);
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        var login = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        var principal = MfaHarness.Principal(login.Data!.Tokens!);
        string resetToken = await scope.Users.GeneratePasswordResetTokenAsync(user);
        interceptor.AfterNextSave = async () =>
        {
            using MfaScope other = harness.Scope();
            var concurrentUser = (await other.Users.FindByIdAsync(user.Id))!;
            Assert.True((await other.Users.AccessFailedAsync(concurrentUser)).Succeeded);
        };
        Response result = await ChangeAsync(scope, user.Id, reset, resetToken, MfaHarness.Password);
        Assert.False(result.IsError);
        using MfaScope fresh = harness.Scope();
        var saved = (await fresh.Users.FindByIdAsync(user.Id))!;
        Assert.True(await fresh.Users.CheckPasswordAsync(saved, "ChangedPassword1!"));
        Assert.Equal(1, saved.AuthenticationVersion);
        Assert.False(await fresh.Sessions.ValidateAsync(principal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedPasswordProofDoesNotLeaveASessionVersionChangeToBeSavedLater(bool reset)
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        Response result = await ChangeAsync(scope, user.Id, reset, "invalid-token", "incorrect-password");
        Assert.True(result.IsError);
        await scope.Db.SaveChangesAsync();
        using MfaScope fresh = harness.Scope();
        var saved = (await fresh.Users.FindByIdAsync(user.Id))!;
        Assert.Equal(0, saved.AuthenticationVersion);
        Assert.True(await fresh.Users.CheckPasswordAsync(saved, MfaHarness.Password));
    }

    private static async Task<Response> ChangeAsync(MfaScope scope, string userId, bool reset, string resetToken, string password)
    {
        if (reset)
            return await new ResetPassword.Handler(scope.Users, scope.Db).ResetPasswordAsync(new()
            { Email = "user@example.com", Token = resetToken, Password = "ChangedPassword1!" });
        var current = (CurrentUser)scope.Services.GetRequiredService<ICurrentUser>();
        current.SetCurrentUserId(userId);
        return await new ChangePassword.Handler(scope.Users, scope.Db, current,
            scope.Services.GetRequiredService<ITenantGetterService>(), new NoJobs()).ChangePasswordAsync(new()
            { Password = password, NewPassword = "ChangedPassword1!", ConfirmNewPassword = "ChangedPassword1!" });
    }

    private sealed class NoJobs : IJobService
    {
        public Task EnqueueAsync<T>(T requestInput, string methodName, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Interloper : SaveChangesInterceptor
    {
        public Func<Task>? AfterNextSave;
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            Func<Task>? action = AfterNextSave;
            AfterNextSave = null;
            if (action is not null) await action();
            return result;
        }
    }
}
