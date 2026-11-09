using System.Net;
using System.Net.Http.Json;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using BackgroundJobs = AditiKraft.Krafter.Backend.Infrastructure.Jobs.Jobs;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class PasswordRecoveryTests
{
    private const string ForgotPath = "/api/users/forgot-password";
    private const string ResetPath = "/api/users/reset-password";
    private const string NewPassword = "RecoveredPassword1!";

    [Fact]
    public async Task ForgotPasswordHasTheSameCompletePublicEnvelopeForKnownUnknownAndIneligibleAccounts()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        ApplicationUser known = await scope.AddUserAsync();
        ApplicationUser disabled = await AddIneligibleUserAsync(scope, deleted: false);
        ApplicationUser deleted = await AddIneligibleUserAsync(scope, deleted: true);
        var jobs = new RecordingJobs();
        await using RecoveryHost host = await RecoveryHost.StartAsync(scope, jobs);

        string? expected = null;
        foreach (string email in new[] { known.Email!, "absent@example.test", disabled.Email!, deleted.Email! })
        {
            using HttpResponseMessage response = await host.Client.PostAsJsonAsync(ForgotPath, new ForgotPasswordRequest { Email = email });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string body = await response.Content.ReadAsStringAsync();
            expected ??= body;
            Assert.Equal(expected, body);
            Response envelope = (await response.Content.ReadFromJsonAsync<Response>())!;
            Assert.False(envelope.IsError);
            Assert.Equal(200, envelope.StatusCode);
            Assert.Equal("If the account is eligible, you will receive a password reset link.", envelope.Message);
        }

        Assert.Equal(known.Email, Assert.Single(jobs.Emails).Email);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnqueueFailureDoesNotRevealAnAccountAndLogsOnlySanitizedFailureDetails(bool dependencyCancellation)
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        ApplicationUser known = await scope.AddUserAsync();
        ApplicationUser disabled = await AddIneligibleUserAsync(scope, deleted: false);
        ApplicationUser deleted = await AddIneligibleUserAsync(scope, deleted: true);
        const string privateDetail = "private-mail-address-and-reset-token-payload";
        Exception failure = dependencyCancellation ? new OperationCanceledException(privateDetail)
            : new InvalidOperationException(privateDetail);
        var jobs = new RecordingJobs { Failure = failure };
        var logger = new RecoveryLogger();
        await using RecoveryHost host = await RecoveryHost.StartAsync(scope, jobs, logger: logger);

        string? expected = null;
        foreach (string email in new[] { known.Email!, "absent@example.test", disabled.Email!, deleted.Email! })
        {
            using HttpResponseMessage response = await host.Client.PostAsJsonAsync(ForgotPath, new ForgotPasswordRequest { Email = email });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string body = await response.Content.ReadAsStringAsync();
            expected ??= body;
            Assert.Equal(expected, body);
            Response envelope = (await response.Content.ReadFromJsonAsync<Response>())!;
            Assert.False(envelope.IsError);
            Assert.Equal("If the account is eligible, you will receive a password reset link.", envelope.Message);
        }

        Assert.Equal(1, jobs.Attempts);
        Assert.Empty(jobs.Emails);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal($"Password reset email could not be queued. Failure type: {failure.GetType().Name}.", entry.Message);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain(privateDetail, entry.Message);
        Assert.DoesNotContain(known.Email!, entry.Message);
    }

    [Fact]
    public async Task InvalidResetHasTheSameCompleteEnvelopeForKnownUnknownAndIneligibleAccounts()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        ApplicationUser known = await scope.AddUserAsync();
        ApplicationUser disabled = await AddIneligibleUserAsync(scope, deleted: false);
        ApplicationUser deleted = await AddIneligibleUserAsync(scope, deleted: true);
        var jobs = new RecordingJobs();
        await using RecoveryHost host = await RecoveryHost.StartAsync(scope, jobs);

        string? expected = null;
        foreach (string email in new[] { known.Email!, "absent@example.test", disabled.Email!, deleted.Email! })
        {
            using HttpResponseMessage response = await host.Client.PostAsJsonAsync(ResetPath, new ResetPasswordRequest
            { Email = email, Token = "invalid-reset-proof", Password = NewPassword });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            string body = await response.Content.ReadAsStringAsync();
            expected ??= body;
            Assert.Equal(expected, body);
            Response envelope = (await response.Content.ReadFromJsonAsync<Response>())!;
            Assert.True(envelope.IsError);
            Assert.Equal(400, envelope.StatusCode);
            Assert.Equal("The password reset link is invalid or expired. Request a new link and try again.", envelope.Message);
        }

        // Even a previously issued proof cannot reset a now-disabled account.
        string disabledProof = await scope.Users.GeneratePasswordResetTokenAsync(disabled);
        using HttpResponseMessage disabledResponse = await host.Client.PostAsJsonAsync(ResetPath, new ResetPasswordRequest
        { Email = disabled.Email, Token = disabledProof, Password = NewPassword });
        Assert.Equal(HttpStatusCode.BadRequest, disabledResponse.StatusCode);
        Assert.Equal(expected, await disabledResponse.Content.ReadAsStringAsync());
        await scope.Db.SaveChangesAsync();
        using MfaScope fresh = harness.Scope();
        ApplicationUser saved = (await fresh.Users.FindByIdAsync(known.Id))!;
        Assert.Equal(0, saved.AuthenticationVersion);
        Assert.True(await fresh.Users.CheckPasswordAsync(saved, MfaHarness.Password));
        Assert.Empty(jobs.Emails);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealRecoveryRoutesEnforceTheNamedAuthenticationLimitBeforeInvokingHandlers(bool reset)
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        ApplicationUser user = await scope.AddUserAsync();
        var jobs = new RecordingJobs();
        const int permits = 2;
        await using RecoveryHost host = await RecoveryHost.StartAsync(scope, jobs, permits);
        string path = reset ? ResetPath : ForgotPath;

        foreach (string recoveryPath in new[] { ForgotPath, ResetPath })
        {
            RouteEndpoint endpoint = Assert.Single(host.Endpoints, endpoint => endpoint.RoutePattern.RawText == recoveryPath);
            Assert.Equal("authentication", endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
            Assert.Empty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
        }

        for (int index = 0; index < permits + 1; index++)
        {
            using HttpResponseMessage response = reset
                ? await host.Client.PostAsJsonAsync(path, new ResetPasswordRequest
                    { Email = user.Email, Token = "invalid-reset-proof", Password = NewPassword })
                : await host.Client.PostAsJsonAsync(path, new ForgotPasswordRequest { Email = user.Email! });
            Assert.Equal(index < permits ? reset ? HttpStatusCode.BadRequest : HttpStatusCode.OK
                : HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        Assert.Equal(reset ? 0 : permits, jobs.Emails.Count);
        if (!reset)
        {
            Assert.True(jobs.EnqueuedCancellation.CanBeCanceled);
            Assert.Equal(host.LastAcceptedCancellation, jobs.EnqueuedCancellation);
        }
    }

    [Fact]
    public async Task ValidResetRevokesExistingSessionsAndKeepsTheEnrolledFactor()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        ApplicationUser user = await scope.AddUserAsync();
        await harness.EnrollAsync(scope, user);
        var login = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "recovery-test");
        var setup = await scope.Mfa.StatusAsync(user.Id);
        Assert.True(setup.Data!.IsEnabled);
        // The management principal represents a session issued before the password reset.
        var oldPrincipal = await harness.ManagementPrincipalAsync(scope, user.Id);
        Assert.True(await scope.Sessions.ValidateAsync(oldPrincipal));
        Assert.NotNull(login.Data!.Challenge);
        user = (await scope.Users.FindByIdAsync(user.Id))!;
        int oldVersion = user.AuthenticationVersion;
        string proof = await scope.Users.GeneratePasswordResetTokenAsync(user);
        await using RecoveryHost host = await RecoveryHost.StartAsync(scope, new RecordingJobs());

        using HttpResponseMessage response = await host.Client.PostAsJsonAsync(ResetPath, new ResetPasswordRequest
        { Email = user.Email, Token = proof, Password = NewPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<Response>())!.IsError);
        using MfaScope fresh = harness.Scope();
        ApplicationUser saved = (await fresh.Users.FindByIdAsync(user.Id))!;
        Assert.True(await fresh.Users.CheckPasswordAsync(saved, NewPassword));
        Assert.Equal(oldVersion + 1, saved.AuthenticationVersion);
        Assert.True(saved.TwoFactorEnabled);
        Assert.False(await fresh.Sessions.ValidateAsync(oldPrincipal));
        var nextLogin = await fresh.Mfa.PasswordLoginAsync(new() { Email = user.Email!, Password = NewPassword }, "recovery-test");
        Assert.NotNull(nextLogin.Data!.Challenge);
        Assert.Null(nextLogin.Data.Tokens);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task APreCanceledRecoveryRequestDoesNoWork(bool reset)
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        ApplicationUser user = await scope.AddUserAsync();
        string proof = await scope.Users.GeneratePasswordResetTokenAsync(user);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var jobs = new RecordingJobs();

        if (reset)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ResetPassword.Handler(scope.Users, scope.Db)
                .ResetPasswordAsync(new() { Email = user.Email, Token = proof, Password = NewPassword }, cancellation.Token));
        else
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ForgotHandler(scope, jobs)
                .ForgotPasswordAsync(new() { Email = user.Email! }, cancellation.Token));

        Assert.Empty(jobs.Emails);
        Assert.Equal(0, user.AuthenticationVersion);
        Assert.True(await scope.Users.CheckPasswordAsync(user, MfaHarness.Password));
    }

    [Fact]
    public async Task CancellationDuringIdentityTokenGenerationPreventsEmailEnqueue()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        ApplicationUser user = await scope.AddUserAsync();
        using var cancellation = new CancellationTokenSource();
        scope.Users.RegisterTokenProvider("cancel-on-generate", new CancelingTokenProvider(cancellation));
        scope.Users.Options.Tokens.PasswordResetTokenProvider = "cancel-on-generate";
        var jobs = new RecordingJobs();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ForgotHandler(scope, jobs)
            .ForgotPasswordAsync(new() { Email = user.Email! }, cancellation.Token));

        Assert.Empty(jobs.Emails);
    }

    [Fact]
    public async Task CallerCancellationDuringEnqueuePropagatesWithoutLoggingAnOperationalFailure()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        ApplicationUser user = await scope.AddUserAsync();
        using var cancellation = new CancellationTokenSource();
        var jobs = new RecordingJobs
        {
            BeforeEnqueue = cancellation.Cancel,
            Failure = new OperationCanceledException(cancellation.Token)
        };
        var logger = new RecoveryLogger();

        OperationCanceledException observed = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ForgotHandler(scope, jobs, logger).ForgotPasswordAsync(new() { Email = user.Email! }, cancellation.Token));

        Assert.Equal(cancellation.Token, observed.CancellationToken);
        Assert.Equal(1, jobs.Attempts);
        Assert.Empty(jobs.Emails);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task CancellationAfterTheIdentitySaveDoesNotReportACommittedResetAsFailed()
    {
        var interceptor = new CancelAfterSave();
        using var harness = new MfaHarness(interceptor: interceptor);
        using MfaScope scope = harness.Scope();
        ApplicationUser user = await scope.AddUserAsync();
        string proof = await scope.Users.GeneratePasswordResetTokenAsync(user);
        using var cancellation = new CancellationTokenSource();
        interceptor.Cancellation = cancellation;

        Response response = await new ResetPassword.Handler(scope.Users, scope.Db).ResetPasswordAsync(new()
        { Email = user.Email, Token = proof, Password = NewPassword }, cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.False(response.IsError);
        using MfaScope fresh = harness.Scope();
        ApplicationUser saved = (await fresh.Users.FindByIdAsync(user.Id))!;
        Assert.Equal(1, saved.AuthenticationVersion);
        Assert.True(await fresh.Users.CheckPasswordAsync(saved, NewPassword));
    }

    private static ForgotPassword.Handler ForgotHandler(MfaScope scope, RecordingJobs jobs,
        ILogger<ForgotPassword>? logger = null) =>
        new(scope.Users, scope.Services.GetRequiredService<ITenantGetterService>(), jobs,
            logger ?? NullLogger<ForgotPassword>.Instance);

    private static async Task<ApplicationUser> AddIneligibleUserAsync(MfaScope scope, bool deleted)
    {
        string email = deleted ? "deleted@example.test" : "disabled@example.test";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(), UserName = email, Email = email,
            IsActive = deleted, IsDeleted = deleted
        };
        Assert.True((await scope.Users.CreateAsync(user, MfaHarness.Password)).Succeeded);
        return user;
    }

    private sealed class RecordingJobs : IJobService
    {
        public List<SendEmailRequestInput> Emails { get; } = [];
        public CancellationToken EnqueuedCancellation { get; private set; }
        public int Attempts { get; private set; }
        public Exception? Failure { get; init; }
        public Action? BeforeEnqueue { get; init; }

        public Task EnqueueAsync<T>(T requestInput, string methodName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(nameof(BackgroundJobs.SendEmailJob), methodName);
            Attempts++;
            EnqueuedCancellation = cancellationToken;
            BeforeEnqueue?.Invoke();
            if (Failure is not null) throw Failure;
            Emails.Add(Assert.IsType<SendEmailRequestInput>(requestInput));
            return Task.CompletedTask;
        }
    }

    private sealed class RecoveryLogger : ILogger<ForgotPassword>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception), exception));
    }

    private sealed class CancelingTokenProvider(CancellationTokenSource cancellation)
        : IUserTwoFactorTokenProvider<ApplicationUser>
    {
        public Task<string> GenerateAsync(string purpose, UserManager<ApplicationUser> manager, ApplicationUser user)
        {
            cancellation.Cancel();
            return Task.FromResult("unused-reset-proof");
        }

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<ApplicationUser> manager, ApplicationUser user) =>
            Task.FromResult(false);

        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<ApplicationUser> manager, ApplicationUser user) =>
            Task.FromResult(false);
    }

    private sealed class CancelAfterSave : SaveChangesInterceptor
    {
        public CancellationTokenSource? Cancellation { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            Cancellation?.Cancel();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RecoveryHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client => client;
        public CancellationToken LastAcceptedCancellation { get; private set; }
        public IEnumerable<RouteEndpoint> Endpoints => ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>();

        public static async Task<RecoveryHost> StartAsync(MfaScope scope, RecordingJobs jobs, int permits = 30,
            ILogger<ForgotPassword>? logger = null)
        {
            WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
            { ApplicationName = typeof(PasswordRecoveryTests).Assembly.GetName().Name, EnvironmentName = "Production" });
            builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Services.AddRouting();
            builder.AddFluentValidationEndpointFilter();
            builder.Services.AddSingleton<IValidator<ForgotPasswordRequest>>(new ForgotPasswordRequestValidator());
            builder.Services.AddSingleton<IValidator<ResetPasswordRequest>>(new ResetPasswordRequestValidator());
            builder.Services.AddSingleton(ForgotHandler(scope, jobs, logger));
            builder.Services.AddSingleton(new ResetPassword.Handler(scope.Users, scope.Db));
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddFixedWindowLimiter("authentication", limiter =>
                {
                    limiter.PermitLimit = permits;
                    limiter.Window = TimeSpan.FromHours(1);
                    limiter.QueueLimit = 0;
                });
            });
            WebApplication app = builder.Build();
            var host = new RecoveryHost(app, new HttpClient());
            app.UseRateLimiter();
            app.Use(async (context, next) =>
            {
                host.LastAcceptedCancellation = context.RequestAborted;
                await next(context);
            });
            RouteGroupBuilder api = app.MapGroup("/api");
            new ForgotPassword.Route().MapRoute(api);
            new ResetPassword.Route().MapRoute(api);
            await app.StartAsync();
            host.Client.BaseAddress = new Uri(app.Urls.Single());
            return host;
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
