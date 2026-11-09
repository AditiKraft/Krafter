using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Common.Constants;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refit;
using AuthenticationService = AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth.AuthenticationService;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class AuthenticationCredentialOrderingTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("password", false)]
    [InlineData("password", true)]
    [InlineData("google", false)]
    [InlineData("google", true)]
    [InlineData("mfa", false)]
    [InlineData("mfa", true)]
    public async Task NewSignInExchangeWaitsForOldCredentialsBeforeReplacingBrowserStorageAndCookies(string flow, bool synchronize)
    {
        await using var session = await BrowserSession.StartAsync();
        await session.PrepareAsync(flow);
        session.Backend.HeldPath = synchronize ? "/api/tokens/current" : "/api/tokens/refresh";
        Task<bool> oldCredentials = synchronize ? session.Tokens.SynchronizeFromServerAsync() : session.Tokens.RefreshAsync();
        await session.Backend.HeldRequestEntered.Task.WaitAsync(Timeout);
        int previousSignInCalls = session.Api.SignInCalls;
        Task<bool> signIn = session.SignInAsync(flow);

        // The observer runs synchronously before entering the actual HTTP adapter. Merely locking
        // CacheAuthTokens would still let the exchange enter here and overwrite HttpOnly cookies.
        bool exchangeEnteredBeforeOldResponse = session.Api.SignInCalls != previousSignInCalls;
        bool signInFinishedBeforeOldResponse = signIn.IsCompleted;
        session.Backend.ReleaseHeldRequest.TrySetResult();
        Assert.True(await oldCredentials.WaitAsync(Timeout));
        Assert.True(await signIn.WaitAsync(Timeout));

        Assert.False(exchangeEnteredBeforeOldResponse);
        Assert.False(signInFinishedBeforeOldResponse);
        Assert.Equal(session.Backend.AccountB.Token, await session.Store.GetCachedAuthTokenAsync());
        Assert.Equal(session.Backend.AccountB.RefreshToken, await session.Store.GetCachedRefreshTokenAsync());
        Assert.Equal(session.Backend.AccountB.Token, session.Cookie(StorageConstants.Local.AuthToken));
        Assert.Equal(session.Backend.AccountB.RefreshToken, session.Cookie(StorageConstants.Local.RefreshToken));
        Assert.Null(session.Authentication.PendingChallenge);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MfaCompletionAndLogoutFollowTheOrderInWhichTheyAcquireTheCoordinator(bool logoutFirst)
    {
        await using var session = await BrowserSession.StartAsync();
        await session.PrepareAsync("mfa");
        session.Backend.HeldPath = logoutFirst ? "/api/tokens/logout" : "/api/two-factor/login";
        Task<bool> first = logoutFirst
            ? session.Authentication.LogoutAsync("ordering regression")
            : session.Authentication.VerifyTwoFactorAsync("test-proof", true);
        await session.Backend.HeldRequestEntered.Task.WaitAsync(Timeout);
        int previousVerifications = session.Api.VerificationCalls;
        Task<bool> second = logoutFirst
            ? session.Authentication.VerifyTwoFactorAsync("test-proof", true)
            : session.Authentication.LogoutAsync("ordering regression");
        bool secondExchangeEnteredEarly = logoutFirst
            ? session.Api.VerificationCalls != previousVerifications
            : session.Api.LogoutCalls != 0;
        session.Backend.ReleaseHeldRequest.TrySetResult();

        Assert.True(await first.WaitAsync(Timeout));
        Assert.Equal(!logoutFirst, await second.WaitAsync(Timeout));
        Assert.False(secondExchangeEnteredEarly);
        Assert.Equal(logoutFirst ? 0 : 1, session.Api.VerificationCalls);
        Assert.Equal(1, session.Api.LogoutCalls);
        Assert.Null(session.Authentication.PendingChallenge);
        Assert.Null(await session.Store.GetCachedAuthTokenAsync());
        Assert.Null(await session.Store.GetCachedRefreshTokenAsync());
        Assert.Null(session.Cookie(StorageConstants.Local.AuthToken));
        Assert.Null(session.Cookie(StorageConstants.Local.RefreshToken));
        Assert.Equal("/login", session.Navigation.Target);
        if (logoutFirst) Assert.Equal("Start sign-in again.", session.Authentication.ErrorMessage);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("google")]
    [InlineData("mfa")]
    public async Task LogoutAlsoWaitsForTheNewBrowserCredentialsToFinishSaving(string flow)
    {
        await using var session = await BrowserSession.StartAsync();
        await session.PrepareAsync(flow);
        var saveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Store.BeforeSave = async _ =>
        {
            saveEntered.TrySetResult();
            await releaseSave.Task;
        };
        Task<bool> signIn = session.SignInAsync(flow);
        Task<bool>? logout = null;
        bool logoutEnteredBeforeSave = false;
        try
        {
            await saveEntered.Task.WaitAsync(Timeout);
            logout = session.Authentication.LogoutAsync("browser storage regression");
            logoutEnteredBeforeSave = session.Api.LogoutCalls != 0;
        }
        finally
        {
            releaseSave.TrySetResult();
            await signIn.WaitAsync(Timeout);
            if (logout is not null) await logout.WaitAsync(Timeout);
        }

        Assert.True(await signIn);
        Assert.NotNull(logout);
        Assert.True(await logout);
        Assert.False(logoutEnteredBeforeSave);
        Assert.Null(await session.Store.GetCachedAuthTokenAsync());
        Assert.Null(await session.Store.GetCachedRefreshTokenAsync());
        Assert.Null(session.Cookie(StorageConstants.Local.AuthToken));
        Assert.Null(session.Cookie(StorageConstants.Local.RefreshToken));
        Assert.Null(session.Authentication.PendingChallenge);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("google")]
    [InlineData("mfa")]
    public async Task SuccessfulSignInNotifiesSubscribersAfterReleasingTheCoordinator(string flow)
    {
        await using var session = await BrowserSession.StartAsync();
        await session.PrepareAsync(flow);
        var callbacks = new List<(string Name, bool FinishedInline, Task<bool> Refresh)>();
        void Observe(string name)
        {
            Task<bool> refresh = session.Tokens.RefreshAsync();
            callbacks.Add((name, refresh.IsCompletedSuccessfully, refresh));
        }
        session.Authentication.LoginChange += _ => Observe("login");
        session.Layout.HeadingChanged += (_, _) => Observe("heading");

        Assert.True(await session.SignInAsync(flow).WaitAsync(Timeout));
        Assert.Equal(new[] { "login", "heading" }, callbacks.Select(callback => callback.Name));
        Assert.All(callbacks, callback => Assert.True(callback.FinishedInline));
        Assert.All(await Task.WhenAll(callbacks.Select(callback => callback.Refresh)).WaitAsync(Timeout), result => Assert.True(result));
        Assert.Equal(1, session.Store.Saves);
        Assert.Equal(0, session.Api.RefreshCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedSignInPreservesFlowStateUntilItsExchangeRunsAndNeverPersistsPendingOrFailedResponses(bool fail)
    {
        await using var session = await BrowserSession.StartAsync();
        await session.PrepareAsync("mfa");
        session.Backend.VerificationResponse = Response<TokenResponse>.Unauthorized("Check your verification code.");
        Assert.False(await session.Authentication.VerifyTwoFactorAsync("test-invalid-proof", false));
        TwoFactorChallengeResponse? originalChallenge = session.Authentication.PendingChallenge;
        session.Backend.PrimaryResponse = fail
            ? Response<LoginResponse>.Unauthorized("Unable to sign in.")
            : Response<LoginResponse>.Success(LoginResponse.Pending("replacement-challenge", DateTimeOffset.UtcNow.AddMinutes(5)));
        int notifications = 0;
        session.Authentication.LoginChange += _ => notifications++;
        session.Layout.HeadingChanged += (_, _) => notifications++;
        session.Backend.HeldPath = "/api/tokens/current";
        Task<bool> current = session.Tokens.SynchronizeFromServerAsync();
        await session.Backend.HeldRequestEntered.Task.WaitAsync(Timeout);
        Task<bool> signIn = session.Authentication.LoginAsync(new());
        TwoFactorChallengeResponse? challengeWhileQueued = session.Authentication.PendingChallenge;
        string? errorWhileQueued = session.Authentication.ErrorMessage;
        bool exchangeEnteredEarly = session.Api.PrimaryCalls != 0;
        session.Backend.ReleaseHeldRequest.TrySetResult();

        Assert.True(await current.WaitAsync(Timeout));
        Assert.False(await signIn.WaitAsync(Timeout));
        Assert.Same(originalChallenge, challengeWhileQueued);
        Assert.Equal("Check your verification code.", errorWhileQueued);
        Assert.False(exchangeEnteredEarly);
        Assert.Equal(1, session.Store.Saves); // Only the old current-token synchronization saved anything.
        Assert.Equal(0, notifications);
        Assert.Equal(session.Original.Token, await session.Store.GetCachedAuthTokenAsync());
        Assert.Equal(session.Original.Token, session.Cookie(StorageConstants.Local.AuthToken));
        if (fail)
        {
            Assert.Null(session.Authentication.PendingChallenge);
            Assert.Equal("Unable to sign in.", session.Authentication.ErrorMessage);
        }
        else
        {
            Assert.Equal("replacement-challenge", session.Authentication.PendingChallenge?.Id);
            Assert.Null(session.Authentication.ErrorMessage);
        }
    }

    [Theory]
    [InlineData("password")]
    [InlineData("google")]
    [InlineData("mfa")]
    public async Task ServerFormFactorDoesNotWriteTheBrowserStorage(string flow)
    {
        await using var session = await BrowserSession.StartAsync("Web");
        await session.PrepareAsync(flow);

        Assert.True(await session.SignInAsync(flow).WaitAsync(Timeout));

        Assert.Equal(0, session.Store.Saves);
        Assert.Equal(session.Original.Token, await session.Store.GetCachedAuthTokenAsync());
        Assert.Equal(session.Backend.AccountB.Token, session.Cookie(StorageConstants.Local.AuthToken));
    }

    [Theory]
    [InlineData("password")]
    [InlineData("google")]
    [InlineData("mfa")]
    public async Task ProviderFailureCleanupCannotClearANewerCompletedSignIn(string flow)
    {
        await using var session = await BrowserSession.StartAsync();
        await session.PrepareAsync(flow);
        session.Backend.RefreshResponse = Response<TokenResponse>.Unauthorized("Old session expired.");
        session.Backend.HeldPath = "/api/tokens/refresh";
        session.Backend.HeldSignInPath = SignInPath(flow);
        UIAuthenticationStateProvider provider = CreateProvider(session);
        Task<AuthenticationState> oldState = provider.GetAuthenticationStateAsync();
        await session.Backend.HeldRequestEntered.Task.WaitAsync(Timeout);
        Task<bool> signIn = session.SignInAsync(flow);
        session.Backend.ReleaseHeldRequest.TrySetResult();
        try
        {
            await session.Backend.HeldSignInEntered.Task.WaitAsync(Timeout);
            // The actual provider has requested cleanup while the new exchange owns the coordinator.
            await session.Observer.AutomaticLogoutRequested.Task.WaitAsync(Timeout);
        }
        finally
        {
            session.Backend.ReleaseHeldSignIn.TrySetResult();
        }

        Assert.True(await signIn.WaitAsync(Timeout));
        AuthenticationState state = await oldState.WaitAsync(Timeout);
        Assert.Equal("account-b", state.User.FindFirst("sub")?.Value);
        Assert.Equal(session.Backend.AccountB.Token, await session.Store.GetCachedAuthTokenAsync());
        Assert.Equal(session.Backend.AccountB.Token, session.Cookie(StorageConstants.Local.AuthToken));
        Assert.Equal(0, session.Api.LogoutCalls);
        Assert.Null(session.Navigation.Target);
        Assert.Null(session.Authentication.PendingChallenge);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProviderFailureCleansAnUnchangedSessionButPreservesAnExistingPendingChallenge(bool pending)
    {
        await using var session = await BrowserSession.StartAsync();
        if (pending)
        {
            await session.PrepareAsync("mfa");
            session.Backend.VerificationResponse = Response<TokenResponse>.Unauthorized("Check your verification code.");
            Assert.False(await session.Authentication.VerifyTwoFactorAsync("test-invalid-proof", false));
        }
        session.Backend.RefreshResponse = Response<TokenResponse>.Unauthorized("Old session expired.");
        UIAuthenticationStateProvider provider = CreateProvider(session);

        AuthenticationState state = await provider.GetAuthenticationStateAsync().WaitAsync(Timeout);

        Assert.False(state.User.Identity?.IsAuthenticated);
        Assert.Equal(1, session.Api.RefreshCalls);
        Assert.Equal(pending ? 0 : 1, session.Api.LogoutCalls);
        if (pending)
        {
            Assert.Equal("account-b-challenge", session.Authentication.PendingChallenge?.Id);
            Assert.Equal("Check your verification code.", session.Authentication.ErrorMessage);
            Assert.Equal(session.Original.Token, await session.Store.GetCachedAuthTokenAsync());
            Assert.Equal(session.Original.Token, session.Cookie(StorageConstants.Local.AuthToken));
            Assert.Null(session.Navigation.Target);
            // Explicit user logout remains unconditional, including when a challenge exists.
            Assert.True(await session.Authentication.LogoutAsync("explicit logout regression").WaitAsync(Timeout));
            Assert.Null(session.Authentication.PendingChallenge);
            Assert.Equal(1, session.Api.LogoutCalls);
        }
        Assert.Null(await session.Store.GetCachedAuthTokenAsync());
        Assert.Null(session.Cookie(StorageConstants.Local.AuthToken));
        Assert.Equal("/login", session.Navigation.Target);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("google")]
    [InlineData("pending")]
    public async Task LoginBoundaryFailureCleanupCannotClearANewerSignInOrPendingFlow(string flow)
    {
        await using var session = await BrowserSession.StartAsync();
        await session.PrepareAsync(flow);
        if (flow == "pending")
            session.Backend.PrimaryResponse = Response<LoginResponse>.Success(LoginResponse.Pending("new-pending-challenge", DateTimeOffset.UtcNow.AddMinutes(5)));
        session.Backend.RejectCurrent = true;
        session.Backend.HeldPath = "/api/tokens/current";
        session.Backend.HeldSignInPath = SignInPath(flow);
        var login = new Login(session.Authentication, session.Tokens, session.Navigation, null!, new AppUrls(), null!);
        // Invoke the actual browser revalidation operation; native rendering skips its browser-only call.
        Task<bool> oldValidation = (Task<bool>)typeof(Login).GetMethod("ValidateExistingSessionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(login, null)!;
        await session.Backend.HeldRequestEntered.Task.WaitAsync(Timeout);
        Task<bool> signIn = session.SignInAsync(flow);
        session.Backend.ReleaseHeldRequest.TrySetResult();
        try
        {
            await session.Backend.HeldSignInEntered.Task.WaitAsync(Timeout);
            await session.Observer.AutomaticLogoutRequested.Task.WaitAsync(Timeout);
        }
        finally
        {
            session.Backend.ReleaseHeldSignIn.TrySetResult();
        }

        Assert.Equal(flow != "pending", await signIn.WaitAsync(Timeout));
        Assert.False(await oldValidation.WaitAsync(Timeout));
        Assert.Equal(0, session.Api.LogoutCalls);
        Assert.Null(session.Navigation.Target);
        Assert.Equal(flow == "pending" ? "new-pending-challenge" : null, session.Authentication.PendingChallenge?.Id);
        string expected = flow == "pending" ? session.Original.Token : session.Backend.AccountB.Token;
        Assert.Equal(expected, await session.Store.GetCachedAuthTokenAsync());
        Assert.Equal(expected, session.Cookie(StorageConstants.Local.AuthToken));
    }

    [Fact]
    public async Task LoginBoundaryUnchangedFailureStillClearsTheSession()
    {
        await using var session = await BrowserSession.StartAsync();
        session.Backend.RejectCurrent = true;
        var login = new Login(session.Authentication, session.Tokens, session.Navigation, null!, new AppUrls(), null!);

        Assert.False(await ((Task<bool>)typeof(Login).GetMethod("ValidateExistingSessionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(login, null)!).WaitAsync(Timeout));

        Assert.Equal(1, session.Api.LogoutCalls);
        Assert.Null(await session.Store.GetCachedAuthTokenAsync());
        Assert.Null(session.Cookie(StorageConstants.Local.AuthToken));
        Assert.Equal("/login", session.Navigation.Target);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("google")]
    [InlineData("pending")]
    public async Task NewSignInDuringAutomaticLogoutTransportSuppressesItsLaterCleanupAndNavigation(string flow)
    {
        await using var session = await BrowserSession.StartAsync();
        if (flow == "pending")
            session.Backend.PrimaryResponse = Response<LoginResponse>.Success(LoginResponse.Pending("new-pending-challenge", DateTimeOffset.UtcNow.AddMinutes(5)));
        session.Backend.RefreshResponse = Response<TokenResponse>.Unauthorized("Old session expired.");
        session.Backend.HeldPath = "/api/tokens/logout";
        UIAuthenticationStateProvider provider = CreateProvider(session);
        var notifications = new List<string?>();
        session.Authentication.LoginChange += notifications.Add;
        Task<AuthenticationState> oldState = provider.GetAuthenticationStateAsync();
        await session.Backend.HeldRequestEntered.Task.WaitAsync(Timeout);
        Task<bool> signIn = session.SignInAsync(flow);
        session.Backend.ReleaseHeldRequest.TrySetResult();

        Assert.Equal(flow != "pending", await signIn.WaitAsync(Timeout));
        await oldState.WaitAsync(Timeout);
        Assert.Equal(1, session.Api.LogoutCalls); // Its cookie deletion preceded the queued new exchange.
        Assert.DoesNotContain(notifications, name => name is null);
        Assert.Null(session.Navigation.Target);
        Assert.Equal(flow == "pending" ? "new-pending-challenge" : null, session.Authentication.PendingChallenge?.Id);
        Assert.Equal(flow == "pending" ? session.Original.Token : session.Backend.AccountB.Token, await session.Store.GetCachedAuthTokenAsync());
        Assert.Equal(flow == "pending" ? null : session.Backend.AccountB.Token, session.Cookie(StorageConstants.Local.AuthToken));
    }

    [Fact]
    public async Task ANewSignInStartedByLogoutNotificationSuppressesTheOldAutomaticNavigation()
    {
        await using var session = await BrowserSession.StartAsync();
        session.Backend.RefreshResponse = Response<TokenResponse>.Unauthorized("Old session expired.");
        UIAuthenticationStateProvider provider = CreateProvider(session);
        Task<bool>? replacement = null;
        session.Authentication.LoginChange += name =>
        {
            if (name is null) replacement = session.SignInAsync("password");
        };

        await provider.GetAuthenticationStateAsync().WaitAsync(Timeout);
        Assert.NotNull(replacement);
        Assert.True(await replacement.WaitAsync(Timeout));
        Assert.Equal(session.Backend.AccountB.Token, await session.Store.GetCachedAuthTokenAsync());
        Assert.Equal(session.Backend.AccountB.Token, session.Cookie(StorageConstants.Local.AuthToken));
        Assert.Null(session.Navigation.Target);
    }

    [Fact]
    public async Task RegisteredSecurityGuardPreservesCredentialsAndErrorsUntilAutomaticCleanupIsAllowed()
    {
        await using var session = await BrowserSession.StartAsync();
        session.Backend.PrimaryResponse = Response<LoginResponse>.Unauthorized("Current sign-in failed.");
        Assert.False(await session.SignInAsync("password"));
        session.Backend.RefreshResponse = Response<TokenResponse>.Unauthorized("Old session expired.");
        using IDisposable blocker = session.SecurityGuard.Register(() => "Save your recovery codes before leaving.");
        UIAuthenticationStateProvider provider = CreateProvider(session);
        int notifications = 0;
        session.Authentication.LoginChange += _ => notifications++;

        AuthenticationState state = await provider.GetAuthenticationStateAsync().WaitAsync(Timeout);

        Assert.False(state.User.Identity?.IsAuthenticated);
        Assert.Equal(0, session.Api.LogoutCalls);
        Assert.Equal(0, notifications);
        Assert.Equal(session.Original.Token, await session.Store.GetCachedAuthTokenAsync());
        Assert.Equal(session.Original.RefreshToken, await session.Store.GetCachedRefreshTokenAsync());
        Assert.Equal(session.Original.Token, session.Cookie(StorageConstants.Local.AuthToken));
        Assert.Equal(session.Original.RefreshToken, session.Cookie(StorageConstants.Local.RefreshToken));
        Assert.Equal("Current sign-in failed.", session.Authentication.ErrorMessage);
        Assert.Null(session.Navigation.Target);

        blocker.Dispose();
        await provider.GetAuthenticationStateAsync().WaitAsync(Timeout);

        Assert.Equal(1, session.Api.LogoutCalls);
        Assert.Equal(1, notifications);
        Assert.Null(await session.Store.GetCachedAuthTokenAsync());
        Assert.Null(session.Cookie(StorageConstants.Local.AuthToken));
        Assert.Null(session.Authentication.ErrorMessage);
        Assert.Equal("/login", session.Navigation.Target);
    }

    [Fact]
    public async Task SecurityGuardAppearingDuringAutomaticLogoutPreservesLaterLocalStateAndNavigation()
    {
        await using var session = await BrowserSession.StartAsync();
        session.Backend.PrimaryResponse = Response<LoginResponse>.Unauthorized("Current sign-in failed.");
        Assert.False(await session.SignInAsync("password"));
        session.Backend.RefreshResponse = Response<TokenResponse>.Unauthorized("Old session expired.");
        session.Backend.HeldPath = "/api/tokens/logout";
        UIAuthenticationStateProvider provider = CreateProvider(session);
        int notifications = 0;
        session.Authentication.LoginChange += _ => notifications++;
        Task<AuthenticationState> oldState = provider.GetAuthenticationStateAsync();
        await session.Backend.HeldRequestEntered.Task.WaitAsync(Timeout);
        using IDisposable blocker = session.SecurityGuard.Register(() => "Please wait for the account security request to finish.");
        session.Backend.ReleaseHeldRequest.TrySetResult();

        AuthenticationState state = await oldState.WaitAsync(Timeout);

        Assert.False(state.User.Identity?.IsAuthenticated);
        Assert.Equal(1, session.Api.LogoutCalls);
        Assert.Equal(0, notifications);
        Assert.Equal(session.Original.Token, await session.Store.GetCachedAuthTokenAsync());
        Assert.Equal(session.Original.RefreshToken, await session.Store.GetCachedRefreshTokenAsync());
        Assert.Equal("Current sign-in failed.", session.Authentication.ErrorMessage);
        Assert.Null(session.Navigation.Target);
        // The already-sent server cookie deletion cannot be undone; protect the page and transient material.
        Assert.Null(session.Cookie(StorageConstants.Local.AuthToken));

        blocker.Dispose();
        await provider.GetAuthenticationStateAsync().WaitAsync(Timeout);
        Assert.Equal(2, session.Api.LogoutCalls);
        Assert.Null(await session.Store.GetCachedAuthTokenAsync());
        Assert.Null(session.Authentication.ErrorMessage);
        Assert.Equal("/login", session.Navigation.Target);
    }

    [Fact]
    public async Task ExplicitLogoutRemainsUnconditionalWithARegisteredSecurityGuard()
    {
        await using var session = await BrowserSession.StartAsync();
        using IDisposable blocker = session.SecurityGuard.Register(() => "Save your recovery codes before leaving.");

        Assert.True(await session.Authentication.LogoutAsync("explicit logout regression").WaitAsync(Timeout));

        Assert.Equal(1, session.Api.LogoutCalls);
        Assert.Null(await session.Store.GetCachedAuthTokenAsync());
        Assert.Null(session.Cookie(StorageConstants.Local.AuthToken));
        Assert.Equal("/login", session.Navigation.Target);
    }

    private static string SignInPath(string flow) => flow switch
    {
        "google" => "/api/external-auth/google",
        "mfa" => "/api/two-factor/login",
        _ => "/api/tokens"
    };

    private static UIAuthenticationStateProvider CreateProvider(BrowserSession session)
    {
        var provider = new UIAuthenticationStateProvider(session.Tokens, session.Store, session.Authentication, null!);
        typeof(UIAuthenticationStateProvider).GetField("_isInitialLoad", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(provider, false);
        return provider;
    }

    private sealed class BrowserSession : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly HttpClient backendClient;
        private readonly HttpClient browser;
        private readonly CookieContainer cookies;
        private readonly Uri secureOrigin;
        public TokenResponse Original { get; }
        public Backend Backend { get; }
        public ObservedApi Api { get; }
        public Storage Store { get; }
        public TokenRefreshCoordinator Coordinator { get; } = new();
        public AuthTokenService Tokens { get; }
        public LayoutService Layout { get; } = new();
        public Navigation Navigation { get; } = new();
        public AccountSecurityNavigationGuard SecurityGuard { get; } = new();
        public LogoutObserver Observer { get; } = new();
        public AuthenticationService Authentication { get; }

        private BrowserSession(WebApplication app, HttpClient backendClient, Backend backend, TokenResponse original, string formFactor)
        {
            this.app = app;
            this.backendClient = backendClient;
            Backend = backend;
            Original = original;
            var origin = new Uri(app.Urls.Single());
            secureOrigin = new UriBuilder(origin) { Scheme = "https" }.Uri;
            cookies = new CookieContainer();
            cookies.Add(origin, new Cookie(StorageConstants.Local.AuthToken, original.Token, "/"));
            cookies.Add(origin, new Cookie(StorageConstants.Local.RefreshToken, original.RefreshToken, "/"));
            browser = new HttpClient(new HttpClientHandler { CookieContainer = cookies }) { BaseAddress = origin };
            Api = new ObservedApi(new ClientAuthApiService(RestService.For<IAuthApi>(browser), NullLogger<ClientAuthApiService>.Instance));
            Store = new Storage(original);
            Tokens = new AuthTokenService(Api, Store, Coordinator);
            Authentication = new AuthenticationService(Api, Tokens, Coordinator, Layout, Store, Navigation,
                new HttpContextAccessor(), new FormFactor(formFactor), SecurityGuard, Observer);
        }

        public static async Task<BrowserSession> StartAsync(string formFactor = "WebAssembly")
        {
            var backend = new Backend();
            var backendClient = new HttpClient(backend) { BaseAddress = new Uri("https://backend.example.test") };
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddHybridCache();
            builder.Services.AddSingleton<IAuthStorageService, AuthStorageServiceServer>();
            builder.Services.AddSingleton(RestService.For<IAuthApi>(backendClient));
            builder.Services.AddSingleton(RestService.For<ITwoFactorApi>(backendClient));
            builder.Services.AddScoped<IAuthApiService, ServerAuthApiService>();
            // The verified-cookie authentication boundary is supplied by this fixture, as in LogoutFailureTests.
            builder.Services.AddAuthentication("test-session")
                .AddScheme<AuthenticationSchemeOptions, CookieSessionHandler>("test-session", _ => { });
            builder.Services.AddAuthorization();
            WebApplication app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<AuthCookieMiddleware>();
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == backend.HeldPath)
                {
                    backend.HeldRequestEntered.TrySetResult();
                    await backend.ReleaseHeldRequest.Task.WaitAsync(context.RequestAborted);
                }
                if (context.Request.Path == backend.HeldSignInPath)
                {
                    backend.HeldSignInEntered.TrySetResult();
                    await backend.ReleaseHeldSignIn.Task.WaitAsync(context.RequestAborted);
                }
                if (context.Request.Path == "/api/tokens/current" && backend.RejectCurrent)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(Response<TokenResponse>.Unauthorized("Old session expired."));
                    return;
                }
                await next(context);
            });
            app.MapAuthTokenBffEndpoints();
            app.MapTwoFactorBffEndpoints();
            try
            {
                await app.StartAsync();
                return new BrowserSession(app, backendClient, backend, TokenTestData.Create("account-a", expired: true), formFactor);
            }
            catch
            {
                await app.DisposeAsync();
                backendClient.Dispose();
                throw;
            }
        }

        public async Task PrepareAsync(string flow)
        {
            if (flow == "mfa")
            {
                Backend.ExternalResponse = Response<LoginResponse>.Success(LoginResponse.Pending("account-b-challenge", DateTimeOffset.UtcNow.AddMinutes(5)));
                Assert.False(await Authentication.LoginAsync(new TokenRequest { IsExternalLogin = true }).WaitAsync(Timeout));
                Assert.NotNull(Authentication.PendingChallenge);
                Assert.Equal(0, Store.Saves);
                Assert.Equal(Original.Token, Cookie(StorageConstants.Local.AuthToken));
            }
        }

        public Task<bool> SignInAsync(string flow) => flow switch
        {
            "mfa" => Authentication.VerifyTwoFactorAsync("test-proof", true),
            "google" => Authentication.LoginAsync(new TokenRequest { IsExternalLogin = true }),
            _ => Authentication.LoginAsync(new())
        };

        public string? Cookie(string name) => cookies.GetCookies(secureOrigin)[name]?.Value;

        public async ValueTask DisposeAsync()
        {
            Backend.ReleaseHeldRequest.TrySetResult();
            Backend.ReleaseHeldSignIn.TrySetResult();
            browser.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
            backendClient.Dispose();
            Coordinator.Dispose();
        }
    }

    private sealed class Backend : HttpMessageHandler
    {
        public TokenResponse AccountB { get; } = TokenTestData.Create("account-b");
        public TokenResponse RefreshedAccountA { get; } = TokenTestData.Create("account-a");
        public string? HeldPath { get; set; }
        public string? HeldSignInPath { get; set; }
        public bool RejectCurrent { get; set; }
        public TaskCompletionSource HeldRequestEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseHeldRequest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource HeldSignInEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseHeldSignIn { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Response<LoginResponse>? PrimaryResponse { get; set; }
        public Response<LoginResponse>? ExternalResponse { get; set; }
        public Response<TokenResponse>? VerificationResponse { get; set; }
        public Response<TokenResponse>? RefreshResponse { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage result = request.RequestUri!.AbsolutePath switch
            {
                "/api/tokens" => Json(PrimaryResponse ?? Response<LoginResponse>.Success(LoginResponse.Completed(AccountB))),
                "/api/external-auth/google" => Json(ExternalResponse ?? Response<LoginResponse>.Success(LoginResponse.Completed(AccountB))),
                "/api/two-factor/login" => Json(VerificationResponse ?? Response<TokenResponse>.Success(AccountB)),
                "/api/tokens/refresh" => Json(RefreshResponse ?? Response<TokenResponse>.Success(RefreshedAccountA)),
                _ => throw new NotSupportedException("Unexpected backend request in credential ordering regression.")
            };
            result.RequestMessage = request;
            return Task.FromResult(result);
        }

        private static HttpResponseMessage Json<T>(Response<T> response) => new((HttpStatusCode)response.StatusCode)
        { Content = JsonContent.Create(response) };
    }

    private sealed class ObservedApi(IAuthApiService inner) : IAuthApiService
    {
        public int PrimaryCalls { get; private set; }
        public int ExternalCalls { get; private set; }
        public int VerificationCalls { get; private set; }
        public int LogoutCalls { get; private set; }
        public int RefreshCalls { get; private set; }
        public int SignInCalls => PrimaryCalls + ExternalCalls + VerificationCalls;
        public Task<Response<LoginResponse>> CreateTokenAsync(TokenRequest request, CancellationToken cancellation)
        { PrimaryCalls++; return inner.CreateTokenAsync(request, cancellation); }
        public Task<Response<LoginResponse>> ExternalAuthAsync(TokenRequest request, CancellationToken cancellation)
        { ExternalCalls++; return inner.ExternalAuthAsync(request, cancellation); }
        public Task<Response<TokenResponse>> CompleteTwoFactorAsync(TwoFactorRequest request, CancellationToken cancellation)
        { VerificationCalls++; return inner.CompleteTwoFactorAsync(request, cancellation); }
        public Task LogoutAsync(CancellationToken cancellation)
        { LogoutCalls++; return inner.LogoutAsync(cancellation); }
        public Task<Response<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellation)
        { RefreshCalls++; return inner.RefreshTokenAsync(request, cancellation); }
        public Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellation) => inner.GetCurrentTokenAsync(cancellation);
    }

    private sealed class Storage(TokenResponse original) : IAuthStorageService
    {
        private TokenResponse? cached = original;
        public int Saves { get; private set; }
        public Func<TokenResponse, Task>? BeforeSave { get; set; }
        public async ValueTask CacheAuthTokens(TokenResponse response)
        {
            if (BeforeSave is not null) await BeforeSave(response);
            cached = response;
            Saves++;
        }
        public Task ClearCacheAsync() { cached = null; return Task.CompletedTask; }
        public ValueTask<string?> GetCachedAuthTokenAsync() => ValueTask.FromResult(cached?.Token);
        public ValueTask<string?> GetCachedRefreshTokenAsync() => ValueTask.FromResult(cached?.RefreshToken);
        public ValueTask<ICollection<string>?> GetCachedPermissionsAsync() => ValueTask.FromResult<ICollection<string>?>(cached?.Permissions);
        public ValueTask<DateTime> GetAuthTokenExpiryDate() => ValueTask.FromResult(cached?.TokenExpiryTime ?? default);
        public ValueTask<DateTime> GetRefreshTokenExpiryDate() => ValueTask.FromResult(cached?.RefreshTokenExpiryTime ?? default);
    }

    private sealed class CookieSessionHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            string.IsNullOrEmpty(Request.Cookies[StorageConstants.Local.AuthToken]) ? AuthenticateResult.NoResult()
                : AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "account-a")], Scheme.Name)), Scheme.Name)));
    }

    private sealed class Navigation : NavigationManager
    {
        public string? Target { get; private set; }
        public Navigation() => Initialize("https://app.example.test/", "https://app.example.test/");
        protected override void NavigateToCore(string uri, bool forceLoad) => Target = uri;
    }

    private sealed class FormFactor(string value) : IFormFactor
    {
        public string GetFormFactor() => value;
        public string GetPlatform() => value;
    }

    private sealed class LogoutObserver : ILogger<AuthenticationService>
    {
        public TaskCompletionSource AutomaticLogoutRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> values && values.Any(value =>
                value.Key == "MethodName" && value.Value is "AuthenticationService Refresh Token 116" or "Session no longer valid"))
            {
                AutomaticLogoutRequested.TrySetResult();
            }
        }
    }
}
