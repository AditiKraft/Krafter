using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Common.Components.Layout;
using AditiKraft.Krafter.UI.Web.Client.Common.Constants;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using Refit;
using AuthenticationService = AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth.AuthenticationService;
using IAuthenticationService = AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth.IAuthenticationService;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class LogoutFailureTests
{
    private const string PublicFailure = "Unable to sign out. Please try again.";

    [Theory]
    [InlineData(BlazorHostingMode.SplitHost)]
    [InlineData(BlazorHostingMode.SingleHost)]
    public async Task FailedBffLogoutKeepsSessionAndPageUntilAConfirmedRetry(BlazorHostingMode mode)
    {
        TokenResponse tokens = TokenTestData.Create();
        bool failLogout = true;
        int logoutRequests = 0;
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHybridCache();
        builder.Services.AddSingleton<IAuthStorageService, AuthStorageServiceServer>();
        using var unusedBackend = new HttpClient(new NoBackendCalls()) { BaseAddress = new Uri("https://backend.example.test") };
        builder.Services.AddSingleton(RestService.For<IAuthApi>(unusedBackend));
        builder.Services.AddScoped<IAuthApiService, ServerAuthApiService>();
        // Authentication itself is not under test: a surviving cookie represents the verified session.
        builder.Services.AddAuthentication("test-session")
            .AddScheme<AuthenticationSchemeOptions, CookieSessionHandler>("test-session", _ => { });
        builder.Services.AddAuthorization();
        await using WebApplication app = builder.Build();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/api/tokens/logout")
            {
                logoutRequests++;
                if (failLogout)
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context.Response.WriteAsync("private-upstream-detail");
                    return;
                }
            }
            await next(context);
        });
        app.UseAuthentication();
        app.UseAuthorization();
        if (mode == BlazorHostingMode.SplitHost) app.MapAuthTokenBffEndpoints();
        else app.MapClientSessionEndpoints();
        await app.StartAsync();
        try
        {
            var origin = new Uri(app.Urls.Single());
            var cookies = new CookieContainer();
            cookies.Add(origin, new Cookie(StorageConstants.Local.AuthToken, tokens.Token, "/"));
            cookies.Add(origin, new Cookie(StorageConstants.Local.RefreshToken, tokens.RefreshToken, "/"));
            using var transport = new HttpClientHandler { CookieContainer = cookies };
            using var browser = new HttpClient(transport) { BaseAddress = origin };
            var api = new ClientAuthApiService(RestService.For<IAuthApi>(browser), NullLogger<ClientAuthApiService>.Instance);
            var storage = new Storage(tokens);
            using var coordinator = new TokenRefreshCoordinator();
            var navigation = new Navigation();
            var authentication = Authentication(api, storage, coordinator, navigation, "WebAssembly");
            int changes = 0;
            authentication.LoginChange += _ => changes++;
            await using var menu = new LogoutMenu(authentication, navigation);
            await menu.RenderAsync();

            await menu.ClickAsync();

            Assert.Equal(1, logoutRequests);
            Assert.Equal(tokens.Token, cookies.GetCookies(origin)[StorageConstants.Local.AuthToken]?.Value);
            Assert.Equal(tokens.Token, await storage.GetCachedAuthTokenAsync());
            Assert.Equal(0, storage.Clears);
            Assert.Equal(0, changes);
            Assert.Null(navigation.Target);
            Assert.Equal(PublicFailure, authentication.ErrorMessage);
            Assert.Contains(menu.Notifications.Messages, message => message.Detail == PublicFailure);
            // This is the credential restoration a premature /login reload would have triggered.
            Response<TokenResponse>? current = await browser.GetFromJsonAsync<Response<TokenResponse>>("/api/tokens/current");
            Assert.Equal(tokens.Token, current?.Data?.Token);
            Assert.Equal(tokens.RefreshToken, current?.Data?.RefreshToken);

            failLogout = false;
            await menu.ClickAsync();

            Assert.Equal(2, logoutRequests);
            Assert.Null(cookies.GetCookies(origin)[StorageConstants.Local.AuthToken]);
            Assert.Null(cookies.GetCookies(origin)[StorageConstants.Local.RefreshToken]);
            Assert.Null(await storage.GetCachedAuthTokenAsync());
            Assert.Equal(1, storage.Clears);
            Assert.Equal(1, changes);
            Assert.Equal("/login", navigation.Target);
            Assert.True(navigation.ForceLoad);
            Assert.Null(authentication.ErrorMessage);
        }
        finally { await app.StopAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NetworkOrTimeoutFailureIsRetryableWithoutDiscardingBrowserSession(bool timeout)
    {
        using var http = new HttpClient(new FailedTransport(timeout)) { BaseAddress = new Uri("https://app.example.test") };
        var api = new ClientAuthApiService(RestService.For<IAuthApi>(http), NullLogger<ClientAuthApiService>.Instance);
        var storage = new Storage(TokenTestData.Create());
        using var coordinator = new TokenRefreshCoordinator();
        var navigation = new Navigation();
        var authentication = Authentication(api, storage, coordinator, navigation, "WebAssembly");
        int changes = 0;
        authentication.LoginChange += _ => changes++;

        Assert.False(await authentication.LogoutAsync("transport regression"));

        Assert.Equal(PublicFailure, authentication.ErrorMessage);
        Assert.Equal(0, changes);
        Assert.Equal(0, storage.Clears);
        Assert.NotNull(await storage.GetCachedAuthTokenAsync());
        Assert.Null(navigation.Target);
    }

    [Fact]
    public async Task FailedServerCookieClearIsContainedAndCanBeRetried()
    {
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddHybridCache();
        await using ServiceProvider services = registrations.BuildServiceProvider();
        using var http = new HttpClient(new NoBackendCalls()) { BaseAddress = new Uri("https://backend.example.test") };
        var storage = new Storage(TokenTestData.Create()) { FailClear = true };
        var api = new ServerAuthApiService(RestService.For<IAuthApi>(http), storage,
            services.GetRequiredService<HybridCache>(), NullLogger<ServerAuthApiService>.Instance);
        using var coordinator = new TokenRefreshCoordinator();
        var navigation = new Navigation();
        var authentication = Authentication(api, storage, coordinator, navigation, "Web");
        int changes = 0;
        authentication.LoginChange += _ => changes++;

        Assert.False(await authentication.LogoutAsync("server regression"));
        Assert.NotNull(await storage.GetCachedAuthTokenAsync());
        Assert.Equal(PublicFailure, authentication.ErrorMessage);
        Assert.Equal(0, changes);
        Assert.Null(navigation.Target);

        storage.FailClear = false;
        Assert.True(await authentication.LogoutAsync("server retry"));
        Assert.Null(await storage.GetCachedAuthTokenAsync());
        Assert.Equal(1, changes);
        Assert.Equal("/login", navigation.Target);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LogoutWaitsForAnInFlightCredentialResponseBeforeClearingTheSession(bool synchronize)
    {
        TokenResponse original = TokenTestData.Create(expired: !synchronize);
        TokenResponse refreshed = TokenTestData.Create();
        var credentialEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCredential = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var localClearEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLocalClear = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHybridCache();
        builder.Services.AddSingleton<IAuthStorageService, AuthStorageServiceServer>();
        using var backend = new HttpClient(new RefreshTransport(refreshed)) { BaseAddress = new Uri("https://backend.example.test") };
        builder.Services.AddSingleton(RestService.For<IAuthApi>(backend));
        builder.Services.AddScoped<IAuthApiService, ServerAuthApiService>();
        builder.Services.AddAuthentication("test-session")
            .AddScheme<AuthenticationSchemeOptions, CookieSessionHandler>("test-session", _ => { });
        builder.Services.AddAuthorization();
        await using WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<AuthCookieMiddleware>();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == (synchronize ? "/api/tokens/current" : "/api/tokens/refresh"))
            {
                credentialEntered.TrySetResult();
                await releaseCredential.Task.WaitAsync(context.RequestAborted);
            }
            await next(context);
        });
        app.MapAuthTokenBffEndpoints();
        await app.StartAsync();
        try
        {
            var origin = new Uri(app.Urls.Single());
            var cookies = new CookieContainer();
            cookies.Add(origin, new Cookie(StorageConstants.Local.AuthToken, original.Token, "/"));
            cookies.Add(origin, new Cookie(StorageConstants.Local.RefreshToken, original.RefreshToken, "/"));
            using var transport = new HttpClientHandler { CookieContainer = cookies };
            using var browser = new HttpClient(transport) { BaseAddress = origin };
            var api = new ObservedApi(new ClientAuthApiService(RestService.For<IAuthApi>(browser), NullLogger<ClientAuthApiService>.Instance));
            var storage = new Storage(original)
            {
                AfterClear = async () =>
                {
                    localClearEntered.TrySetResult();
                    await releaseLocalClear.Task;
                }
            };
            using var coordinator = new TokenRefreshCoordinator();
            var tokenService = new AuthTokenService(api, storage, coordinator);
            var navigation = new Navigation();
            var authentication = Authentication(api, storage, coordinator, navigation, "WebAssembly");
            Task<bool> credential = synchronize ? tokenService.SynchronizeFromServerAsync() : tokenService.RefreshAsync();
            await credentialEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Task<bool> logout = authentication.LogoutAsync("credential race regression");

            // Calling an async method runs synchronously until its first incomplete await.
            // The observer detects entering the actual adapter while the token coordinator is held;
            // no timer or transport scheduling assumption decides the interleaving below.
            bool beganLogoutBeforeCredentialsFinished = api.LogoutEntered;
            if (beganLogoutBeforeCredentialsFinished)
            {
                await localClearEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Null(cookies.GetCookies(origin)[StorageConstants.Local.AuthToken]);
            }
            releaseCredential.TrySetResult();
            Assert.True(await credential.WaitAsync(TimeSpan.FromSeconds(10)));
            releaseLocalClear.TrySetResult();
            Assert.True(await logout.WaitAsync(TimeSpan.FromSeconds(10)));

            var secureOrigin = new UriBuilder(origin) { Scheme = "https" }.Uri;
            if (beganLogoutBeforeCredentialsFinished)
            {
                // Prove the losing interleaving restored state before failing the ordering assertion.
                Assert.Equal(synchronize ? original.Token : refreshed.Token, await storage.GetCachedAuthTokenAsync());
                if (!synchronize)
                {
                    // AuthCookieMiddleware writes Secure cookies, which an HTTP-origin lookup hides.
                    Assert.Equal(refreshed.Token, cookies.GetCookies(secureOrigin)[StorageConstants.Local.AuthToken]?.Value);
                }
            }
            Assert.False(beganLogoutBeforeCredentialsFinished);
            Assert.Null(await storage.GetCachedAuthTokenAsync());
            Assert.Null(await storage.GetCachedRefreshTokenAsync());
            Assert.Null(cookies.GetCookies(secureOrigin)[StorageConstants.Local.AuthToken]);
            Assert.Null(cookies.GetCookies(secureOrigin)[StorageConstants.Local.RefreshToken]);
            Assert.Equal("/login", navigation.Target);
        }
        finally
        {
            releaseCredential.TrySetResult();
            releaseLocalClear.TrySetResult();
            await app.StopAsync();
        }
    }

    private static AuthenticationService Authentication(IAuthApiService api, Storage storage,
        TokenRefreshCoordinator coordinator, Navigation navigation, string formFactor) =>
        new(api, new AuthTokenService(api, storage, coordinator), coordinator, new LayoutService(), storage, navigation,
            new HttpContextAccessor(), new FormFactor(formFactor), new AccountSecurityNavigationGuard(), NullLogger<AuthenticationService>.Instance);

    private sealed class LogoutMenu : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly HtmlRenderer renderer;
        private readonly Components components;
        public NotificationService Notifications { get; } = new();

        public LogoutMenu(IAuthenticationService authentication, Navigation navigation)
        {
            var registrations = new ServiceCollection();
            registrations.AddLogging();
            registrations.AddAuthorizationCore();
            registrations.AddSingleton<NavigationManager>(navigation);
            registrations.AddSingleton<IJSRuntime, NoJavaScript>();
            registrations.AddSingleton(authentication);
            registrations.AddSingleton<AccountSecurityNavigationGuard>();
            registrations.AddSingleton(Notifications);
            registrations.AddSingleton<Components>();
            registrations.AddSingleton<IComponentActivator>(provider => provider.GetRequiredService<Components>());
            services = registrations.BuildServiceProvider();
            components = services.GetRequiredService<Components>();
            renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        }

        public Task RenderAsync() => renderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment content = builder => { builder.OpenComponent<TopRight>(0); builder.CloseComponent(); };
            await renderer.RenderComponentAsync<CascadingValue<Task<AuthenticationState>>>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(CascadingValue<Task<AuthenticationState>>.Value)] = Task.FromResult(new AuthenticationState(Principal())),
                    [nameof(CascadingValue<Task<AuthenticationState>>.ChildContent)] = content
                }));
        });

        public Task ClickAsync() => renderer.Dispatcher.InvokeAsync(() => components.All.OfType<RadzenSplitButton>().Single()
            .Click.InvokeAsync(components.All.OfType<RadzenSplitButtonItem>().Single(item => item.Value == "Logout")));

        public async ValueTask DisposeAsync()
        {
            await renderer.DisposeAsync();
            await services.DisposeAsync();
        }
    }

    private static ClaimsPrincipal Principal() => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "alice")], "test-session"));

    private sealed class CookieSessionHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            string.IsNullOrEmpty(Request.Cookies[StorageConstants.Local.AuthToken]) ? AuthenticateResult.NoResult()
                : AuthenticateResult.Success(new AuthenticationTicket(Principal(), Scheme.Name)));
    }

    private sealed class Components(IServiceProvider services) : IComponentActivator
    {
        public List<IComponent> All { get; } = [];
        public IComponent CreateInstance(Type type)
        {
            var component = (IComponent)ActivatorUtilities.CreateInstance(services, type);
            All.Add(component);
            return component;
        }
    }

    private sealed class Storage(TokenResponse tokens) : IAuthStorageService
    {
        private TokenResponse? cached = tokens;
        public int Clears { get; private set; }
        public bool FailClear { get; set; }
        public Func<Task>? AfterClear { get; init; }
        public async Task ClearCacheAsync()
        {
            if (FailClear) throw new InvalidOperationException("private-server-detail");
            Clears++;
            cached = null;
            if (AfterClear is not null) await AfterClear();
        }
        public ValueTask<string?> GetCachedAuthTokenAsync() => ValueTask.FromResult(cached?.Token);
        public ValueTask<string?> GetCachedRefreshTokenAsync() => ValueTask.FromResult(cached?.RefreshToken);
        public ValueTask<ICollection<string>?> GetCachedPermissionsAsync() => ValueTask.FromResult<ICollection<string>?>(cached?.Permissions);
        public ValueTask<DateTime> GetAuthTokenExpiryDate() => ValueTask.FromResult(cached?.TokenExpiryTime ?? default);
        public ValueTask<DateTime> GetRefreshTokenExpiryDate() => ValueTask.FromResult(cached?.RefreshTokenExpiryTime ?? default);
        public ValueTask CacheAuthTokens(TokenResponse response) { cached = response; return ValueTask.CompletedTask; }
    }

    private sealed class Navigation : NavigationManager
    {
        public string? Target { get; private set; }
        public bool ForceLoad { get; private set; }
        public Navigation() => Initialize("https://app.example.test/", "https://app.example.test/");
        protected override void NavigateToCore(string uri, bool forceLoad) { Target = uri; ForceLoad = forceLoad; }
    }

    private sealed class FormFactor(string value) : IFormFactor
    {
        public string GetFormFactor() => value;
        public string GetPlatform() => value;
    }

    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }

    private sealed class NoBackendCalls : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Cookie logout and current-token routes must not call the backend.");
    }

    private sealed class FailedTransport(bool timeout) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            timeout ? throw new TaskCanceledException("private-timeout-detail") : throw new HttpRequestException("private-network-detail");
    }

    private sealed class RefreshTransport(TokenResponse tokens) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/api/tokens/refresh", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(Response<TokenResponse>.Success(tokens)) });
        }
    }

    private sealed class ObservedApi(IAuthApiService inner) : IAuthApiService
    {
        public bool LogoutEntered { get; private set; }
        public Task LogoutAsync(CancellationToken cancellation)
        {
            LogoutEntered = true;
            return inner.LogoutAsync(cancellation);
        }
        public Task<Response<LoginResponse>> CreateTokenAsync(TokenRequest request, CancellationToken cancellation) => inner.CreateTokenAsync(request, cancellation);
        public Task<Response<LoginResponse>> ExternalAuthAsync(TokenRequest request, CancellationToken cancellation) => inner.ExternalAuthAsync(request, cancellation);
        public Task<Response<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellation) => inner.RefreshTokenAsync(request, cancellation);
        public Task<Response<TokenResponse>> CompleteTwoFactorAsync(TwoFactorRequest request, CancellationToken cancellation) => inner.CompleteTwoFactorAsync(request, cancellation);
        public Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellation) => inner.GetCurrentTokenAsync(cancellation);
    }
}
