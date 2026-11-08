using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class PasswordLengthTests
{
    [Theory]
    [InlineData("login", 1024, true)]
    [InlineData("login", 1025, false)]
    [InlineData("enroll", 1024, true)]
    [InlineData("enroll", 1025, false)]
    [InlineData("change", 1024, true)]
    [InlineData("change", 1025, false)]
    [InlineData("reset", 1024, true)]
    [InlineData("reset", 1025, false)]
    public void PasswordBoundariesAgreeAcrossPublicRequests(string operation, int length, bool accepted)
    {
        string password = Password(length);
        bool valid = operation switch
        {
            "login" => new TokenRequestValidator().Validate(new TokenRequest { Email = "user@example.com", Password = password }).IsValid,
            "enroll" => new TwoFactorManagementRequestValidator().Validate(new TwoFactorManagementRequest { Password = password }).IsValid,
            "change" => new ChangePasswordRequestValidator().Validate(new ChangePasswordRequest
            { Password = MfaHarness.Password, NewPassword = password, ConfirmNewPassword = password }).IsValid,
            _ => new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest { Email = "user@example.com", Token = "reset-proof", Password = password }).IsValid
        };
        Assert.Equal(accepted, valid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptedBoundaryPasswordsWorkForLoginAndEnrollment(bool reset)
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        string password = Password(1024);
        Response changed = reset
            ? await new ResetPassword.Handler(scope.Users, scope.Db).ResetPasswordAsync(new()
            { Email = user.Email, Token = await scope.Users.GeneratePasswordResetTokenAsync(user), Password = password })
            : await ChangeAsync(scope, user.Id, MfaHarness.Password, password);
        Assert.False(changed.IsError);
        var login = await scope.Mfa.PasswordLoginAsync(new() { Email = user.Email!, Password = password }, "test");
        Assert.False(login.IsError);
        Assert.NotNull(login.Data!.Tokens);
        var setup = await scope.Mfa.BeginEnrollmentAsync(user.Id, new() { Password = password }, new ClaimsPrincipal());
        Assert.False(setup.IsError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedPasswordsAreRejectedByRealRoutesWithoutChangingPasswordOrSession(bool reset)
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        var login = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        var principal = MfaHarness.Principal(login.Data!.Tokens!);
        string resetToken = await scope.Users.GeneratePasswordResetTokenAsync(user);
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        // The real route/filter invokes the handlers against this isolated fixture's store.
        builder.Services.AddSingleton(ChangeHandler(scope, user.Id));
        builder.Services.AddSingleton(new ResetPassword.Handler(scope.Users, scope.Db));
        builder.Services.AddValidatorsFromAssemblyContaining<TokenRequestValidator>();
        builder.AddFluentValidationEndpointFilter();
        builder.Services.AddAuthorization();
        await using WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        new ChangePassword.Route().MapRoute(app.MapGroup("/api").AllowAnonymous());
        new ResetPassword.Route().MapRoute(app.MapGroup("/api"));
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            string password = Password(1025);
            using HttpResponseMessage response = reset
                ? await client.PostAsJsonAsync("/api/users/reset-password", new ResetPasswordRequest
                { Email = user.Email, Token = resetToken, Password = password })
                : await client.PostAsJsonAsync("/api/users/change-password", new ChangePasswordRequest
                { Password = MfaHarness.Password, NewPassword = password, ConfirmNewPassword = password });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using MfaScope fresh = harness.Scope();
            var saved = (await fresh.Users.FindByIdAsync(user.Id))!;
            Assert.True(await fresh.Users.CheckPasswordAsync(saved, MfaHarness.Password));
            Assert.Equal(0, saved.AuthenticationVersion);
            Assert.True(await fresh.Sessions.ValidateAsync(principal));
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task ExistingLongCurrentPasswordCanBeChangedToACompliantPassword()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        string legacy = Password(1025);
        Assert.True((await scope.Users.ChangePasswordAsync(user, MfaHarness.Password, legacy)).Succeeded);
        Assert.True((await scope.Mfa.PasswordLoginAsync(new() { Email = user.Email!, Password = legacy }, "test")).IsError);
        var request = new ChangePasswordRequest
        { Password = legacy, NewPassword = MfaHarness.Password, ConfirmNewPassword = MfaHarness.Password };
        Assert.True(new ChangePasswordRequestValidator().Validate(request).IsValid);
        Assert.False((await ChangeAsync(scope, user.Id, legacy, MfaHarness.Password)).IsError);
        Assert.False((await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test")).IsError);
    }

    private static string Password(int length) => "Aa1!" + new string('x', length - 4);
    private static Task<Response> ChangeAsync(MfaScope scope, string id, string currentPassword, string password) =>
        ChangeHandler(scope, id).ChangePasswordAsync(new()
        { Password = currentPassword, NewPassword = password, ConfirmNewPassword = password });

    private static ChangePassword.Handler ChangeHandler(MfaScope scope, string id)
    {
        var current = (CurrentUser)scope.Services.GetRequiredService<ICurrentUser>();
        current.SetCurrentUserId(id);
        return new ChangePassword.Handler(scope.Users, scope.Db, current,
            scope.Services.GetRequiredService<ITenantGetterService>(), new NoJobs());
    }
    private sealed class NoJobs : IJobService
    {
        public Task EnqueueAsync<T>(T requestInput, string methodName, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
