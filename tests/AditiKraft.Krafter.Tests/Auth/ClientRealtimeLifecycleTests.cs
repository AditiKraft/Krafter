using System.Security.Claims;
using System.Threading.Channels;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Common.Components.Layout;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.SignalR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AditiKraft.Krafter.Tests.Auth;

// TenantInfo is process-wide browser configuration; these transport fixtures temporarily replace it.
[CollectionDefinition("Client realtime configuration", DisableParallelization = true)]
public sealed class ClientRealtimeConfigurationCollection;

[Collection("Client realtime configuration")]
public sealed class ClientRealtimeLifecycleTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(true, false, "WebAssembly")]
    [InlineData(false, true, "WebAssembly")]
    [InlineData(false, false, "WebAssembly")]
    [InlineData(true, true, "Web")]
    public async Task IneligibleDisplayPrincipalNeverReadsCredentialsOrConnects(bool authenticated, bool permission, string formFactor)
    {
        await using TestHost host = await TestHost.StartAsync(403);
        var state = new StateProvider(authenticated, permission);
        var storage = new Storage();
        var logger = new FailureLogger();
        using var coordinator = new TokenRefreshCoordinator();
        await using var service = new SignalRService(new AuthTokenService(new TokenApi(), storage, coordinator), storage, new FormFactor(formFactor), state, logger);

        await service.InitializeAsync().WaitAsync(Timeout);

        Assert.False(service.IsConnected());
        Assert.Equal(0, host.Observations.Requests);
        Assert.Equal(0, storage.Reads);
        Assert.False(logger.Failures.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(503)]
    public async Task DeniedOrUnavailableHubDoesNotEscapeInitialRenderOrLoginChange(int status)
    {
        await using TestHost host = await TestHost.StartAsync(status);
        var authentication = new Authentication();
        var state = new StateProvider(true, true);
        var logger = new FailureLogger();
        var storage = new Storage();
        using var coordinator = new TokenRefreshCoordinator();
        await using var service = new SignalRService(new AuthTokenService(new TokenApi(), storage, coordinator), storage, new FormFactor(), state, logger);
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddSingleton<IAuthenticationService>(authentication);
        registrations.AddSingleton(service);
        registrations.AddSingleton<IComponentActivator, ComponentActivator>();
        await using ServiceProvider services = registrations.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        await renderer.Dispatcher.InvokeAsync(async () => await renderer.RenderComponentAsync<InitializeSignalr>())
            .WaitAsync(Timeout);
        Assert.False(service.IsConnected());
        Assert.Equal(1, authentication.SubscriberCount);
        Assert.True(host.Observations.Requests > 0);
        Assert.Null((await logger.Failures.Reader.ReadAsync().AsTask().WaitAsync(Timeout)).Exception);

        int initialRequests = host.Observations.Requests;
        await renderer.Dispatcher.InvokeAsync(authentication.RaiseLoginChange);
        Assert.Null((await logger.Failures.Reader.ReadAsync().AsTask().WaitAsync(Timeout)).Exception);
        Assert.True(host.Observations.Requests > initialRequests);
        Assert.False(service.IsConnected());
        await renderer.DisposeAsync();
        Assert.Equal(0, authentication.SubscriberCount);
        int stateReads = state.Reads;
        authentication.RaiseLoginChange();
        Assert.Equal(stateReads, state.Reads);
        Assert.Equal(0, authentication.Logouts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DependencyFailureIsContainedAndALaterInitializationCanConnect(bool stateFailure)
    {
        await using TestHost host = await TestHost.StartAsync();
        var state = new StateProvider(true, true) { Fail = stateFailure };
        var storage = new Storage { Fail = !stateFailure };
        var logger = new FailureLogger();
        using var coordinator = new TokenRefreshCoordinator();
        await using var service = new SignalRService(new AuthTokenService(new TokenApi(), storage, coordinator), storage, new FormFactor(), state, logger);

        await service.InitializeAsync().WaitAsync(Timeout);
        Assert.False(service.IsConnected());
        Failure failure = await logger.Failures.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
        Assert.Null(failure.Exception);
        Assert.DoesNotContain("secret-test-credential", failure.Message);

        state.Fail = storage.Fail = false;
        await service.InitializeAsync().WaitAsync(Timeout);
        Assert.True(service.IsConnected());
        await host.Observations.Connected.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
    }

    [Fact]
    public async Task ReinitializingAndLosingDisplayPermissionReleasePreviousSockets()
    {
        await using TestHost host = await TestHost.StartAsync();
        var state = new StateProvider(true, true);
        var storage = new Storage();
        using var coordinator = new TokenRefreshCoordinator();
        await using var service = new SignalRService(new AuthTokenService(new TokenApi(), storage, coordinator), storage, new FormFactor(), state, new FailureLogger());

        await service.InitializeAsync().WaitAsync(Timeout);
        string first = await host.Observations.Connected.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
        await service.InitializeAsync().WaitAsync(Timeout);
        string second = await host.Observations.Connected.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
        Assert.NotEqual(first, second);
        Assert.Equal(first, await host.Observations.Disconnected.Reader.ReadAsync().AsTask().WaitAsync(Timeout));
        Assert.True(service.IsConnected());

        int requests = host.Observations.Requests;
        state.Set(true, false);
        await service.InitializeAsync().WaitAsync(Timeout);
        Assert.Equal(second, await host.Observations.Disconnected.Reader.ReadAsync().AsTask().WaitAsync(Timeout));
        Assert.False(service.IsConnected());
        Assert.Equal(requests, host.Observations.Requests);

        state.Set(true, true);
        await service.InitializeAsync().WaitAsync(Timeout);
        string third = await host.Observations.Connected.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
        await service.DisposeAsync();
        Assert.Equal(third, await host.Observations.Disconnected.Reader.ReadAsync().AsTask().WaitAsync(Timeout));
        Assert.False(service.IsConnected());
        await service.DisposeAsync();
        requests = host.Observations.Requests;
        await service.InitializeAsync();
        Assert.Equal(requests, host.Observations.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupersedingOrDisposingAnInFlightStartCancelsItsHttpRequest(bool dispose)
    {
        await using TestHost host = await TestHost.StartAsync(stall: true);
        var state = new StateProvider(true, true);
        var logger = new FailureLogger();
        var storage = new Storage();
        using var coordinator = new TokenRefreshCoordinator();
        await using var service = new SignalRService(new AuthTokenService(new TokenApi(), storage, coordinator), storage, new FormFactor(), state, logger);
        Task initialization = service.InitializeAsync();
        try
        {
            await host.Observations.RequestStarted.Task.WaitAsync(Timeout);
            if (dispose)
            {
                await service.DisposeAsync().AsTask().WaitAsync(Timeout);
            }
            else
            {
                state.Set(true, false);
                await service.InitializeAsync().WaitAsync(Timeout);
            }

            await initialization.WaitAsync(Timeout);
            await host.Observations.RequestCanceled.Task.WaitAsync(Timeout);
            Assert.Equal(1, host.Observations.Requests);
            Assert.False(service.IsConnected());
            Assert.False(logger.Failures.Reader.TryRead(out _));
        }
        finally
        {
            // An uncancellable regression must fail without stranding the test host or disposal.
            host.Observations.ReleaseRequest.TrySetResult();
            await Record.ExceptionAsync(() => initialization.WaitAsync(Timeout));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SupersedingOrDisposingCancelsHeldCredentialsBeforeAnyHttpRequest(bool refresh, bool dispose)
    {
        await using TestHost host = await TestHost.StartAsync();
        var gate = new HeldCredentialOperation();
        gate.Hold(refresh);
        var state = new StateProvider(true, true);
        var logger = new FailureLogger();
        using var coordinator = new TokenRefreshCoordinator();
        await using var service = new SignalRService(new AuthTokenService(gate.Api, gate.Storage, coordinator), gate.Storage, new FormFactor(), state, logger);
        Task initialization = service.InitializeAsync();
        try
        {
            await gate.Entered.Task.WaitAsync(Timeout);
            Assert.Equal(0, host.Observations.Requests);
            if (dispose) await service.DisposeAsync().AsTask().WaitAsync(Timeout);
            else
            {
                state.Set(true, false);
                await service.InitializeAsync().WaitAsync(Timeout);
            }
            await initialization.WaitAsync(Timeout);
            Assert.False(gate.Operation.IsCompleted);
            Assert.True(await coordinator.ExecuteAsync(() => Task.FromResult(true)).WaitAsync(Timeout));
            if (refresh) Assert.True(gate.RefreshCancellation.IsCancellationRequested);
            Assert.False(service.IsConnected());
            Assert.False(logger.Failures.Reader.TryRead(out _));

            gate.Release();
            await gate.Operation.WaitAsync(Timeout);
            Assert.Equal(0, host.Observations.Requests);
            Assert.Equal(0, gate.Storage.Saves);
            Assert.Equal(0, gate.Storage.Clears);
            Assert.Equal(0, gate.Api.Logouts);
        }
        finally
        {
            gate.Release();
            await gate.Operation.WaitAsync(Timeout);
            await Record.ExceptionAsync(() => initialization.WaitAsync(Timeout));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConnectionLifetimeCancelsHeldCredentialsDuringAutomaticReconnect(bool refresh)
    {
        await using TestHost host = await TestHost.StartAsync();
        var gate = new HeldCredentialOperation();
        var logger = new FailureLogger();
        using var coordinator = new TokenRefreshCoordinator();
        await using var service = new SignalRService(new AuthTokenService(gate.Api, gate.Storage, coordinator), gate.Storage,
            new FormFactor(), new StateProvider(true, true), logger);
        await service.InitializeAsync().WaitAsync(Timeout);
        await host.Observations.Connected.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
        Action abortTransport = await host.Observations.AbortTransport.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
        gate.Hold(refresh);
        try
        {
            // Abrupt HTTP transport loss allows reconnect. A delivered Hub Context.Abort close does not.
            abortTransport();
            await gate.Entered.Task.WaitAsync(Timeout);
            int requests = host.Observations.Requests;
            await service.DisposeAsync().AsTask().WaitAsync(Timeout);
            Assert.False(gate.Operation.IsCompleted);
            Assert.True(await coordinator.ExecuteAsync(() => Task.FromResult(true)).WaitAsync(Timeout));
            if (refresh) Assert.True(gate.RefreshCancellation.IsCancellationRequested);
            gate.Release();
            await gate.Operation.WaitAsync(Timeout);
            Assert.False(service.IsConnected());
            Assert.Equal(requests, host.Observations.Requests);
            Assert.Equal(0, gate.Storage.Saves);
            Assert.Equal(0, gate.Storage.Clears);
            Assert.False(logger.Failures.Reader.TryRead(out _));
        }
        finally
        {
            gate.Release();
            await gate.Operation.WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task StartupDeadlineReleasesAHeldCredentialReadWithoutWaitingForStorage()
    {
        await using TestHost host = await TestHost.StartAsync();
        var gate = new HeldCredentialOperation();
        gate.Hold(refresh: false);
        using var coordinator = new TokenRefreshCoordinator();
        await using var service = new SignalRService(new AuthTokenService(gate.Api, gate.Storage, coordinator), gate.Storage,
            new FormFactor(), new StateProvider(true, true), new FailureLogger());
        Task initialization = service.InitializeAsync();
        try
        {
            await gate.Entered.Task.WaitAsync(Timeout);
            // Exercise the production fifteen-second deadline; no caller cancellation releases it.
            await initialization.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.False(gate.Operation.IsCompleted);
            Assert.False(service.IsConnected());
            Assert.Equal(0, host.Observations.Requests);
        }
        finally
        {
            gate.Release();
            await gate.Operation.WaitAsync(Timeout);
            await Record.ExceptionAsync(() => initialization.WaitAsync(Timeout));
        }
    }

    private sealed class HeldCredentialOperation
    {
        private readonly TaskCompletionSource<TokenResponse> released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Storage Storage { get; } = new();
        public TokenApi Api { get; } = new();
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Operation { get; private set; } = Task.CompletedTask;
        public CancellationToken RefreshCancellation { get; private set; }

        public void Hold(bool refresh)
        {
            if (refresh)
            {
                Storage.Tokens = TokenTestData.Create(expired: true);
                Api.Refresh = cancellation =>
                {
                    RefreshCancellation = cancellation;
                    Task<Response<TokenResponse>> operation = HeldRefreshAsync();
                    Operation = operation;
                    Entered.TrySetResult();
                    return operation;
                };
            }
            else
            {
                Storage.ReadToken = () =>
                {
                    Task<string?> operation = HeldReadAsync();
                    Operation = operation;
                    Entered.TrySetResult();
                    return new ValueTask<string?>(operation);
                };
            }
        }

        public void Release() => released.TrySetResult(TokenTestData.Create());
        private async Task<string?> HeldReadAsync() => (await released.Task).Token;
        private async Task<Response<TokenResponse>> HeldRefreshAsync() => Response<TokenResponse>.Success(await released.Task);
    }

    internal sealed class TestHost(WebApplication app, string previousHost, string previousTenant) : IAsyncDisposable
    {
        public ConnectionObservations Observations => app.Services.GetRequiredService<ConnectionObservations>();

        public static async Task<TestHost> StartAsync(int? status = null, bool stall = false)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<ConnectionObservations>();
            WebApplication app = builder.Build();
            var observations = app.Services.GetRequiredService<ConnectionObservations>();
            app.Use(async (context, next) =>
            {
                Interlocked.Increment(ref observations.Requests);
                observations.RequestStarted.TrySetResult();
                if (stall)
                {
                    try { await observations.ReleaseRequest.Task.WaitAsync(context.RequestAborted); }
                    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                    { observations.RequestCanceled.TrySetResult(); }
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    return;
                }
                if (status is not null)
                {
                    context.Response.StatusCode = status.Value;
                    return;
                }
                await next(context);
            });
            app.MapHub<ClientTestHub>("/api/RealtimeHub");
            await app.StartAsync();
            var host = new TestHost(app, TenantInfo.HostUrl, TenantInfo.Identifier);
            TenantInfo.HostUrl = app.Urls.Single();
            TenantInfo.Identifier = "test";
            return host;
        }

        public async ValueTask DisposeAsync()
        {
            TenantInfo.HostUrl = previousHost;
            TenantInfo.Identifier = previousTenant;
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    public sealed class ConnectionObservations
    {
        public int Requests;
        public Channel<string> Connected { get; } = Channel.CreateUnbounded<string>();
        public Channel<string> Disconnected { get; } = Channel.CreateUnbounded<string>();
        public Channel<Action> AbortTransport { get; } = Channel.CreateUnbounded<Action>();
        public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource RequestCanceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseRequest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed class ClientTestHub(ConnectionObservations observations) : Hub
    {
        public override Task OnConnectedAsync()
        {
            HttpContext context = Context.GetHttpContext()!;
            observations.AbortTransport.Writer.TryWrite(context.Abort);
            return observations.Connected.Writer.WriteAsync(Context.ConnectionId).AsTask();
        }
        public override Task OnDisconnectedAsync(Exception? exception) => observations.Disconnected.Writer.WriteAsync(Context.ConnectionId).AsTask();
    }

    internal sealed class StateProvider : AuthenticationStateProvider
    {
        private AuthenticationState state = new(new ClaimsPrincipal(new ClaimsIdentity()));
        public int Reads { get; private set; }
        public bool Fail { get; set; }
        public StateProvider(bool authenticated, bool permission) => Set(authenticated, permission);
        public void Set(bool authenticated, bool permission) => state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            permission ? [new Claim(AppClaimTypes.Permission, PermissionDefinition.NameFor(PermissionAction.View, PermissionResource.Notifications))] : [],
            authenticated ? "test" : null)));
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            Reads++;
            return Fail ? Task.FromException<AuthenticationState>(new InvalidOperationException("secret-test-credential")) : Task.FromResult(state);
        }
    }

    internal sealed class Storage : IAuthStorageService
    {
        public TokenResponse Tokens { get; set; } = TokenTestData.Create();
        public Func<ValueTask<string?>>? ReadToken { get; set; }
        public bool Fail { get; set; }
        public int Reads { get; private set; }
        public int Saves { get; private set; }
        public int Clears { get; private set; }
        public ValueTask<string?> GetCachedAuthTokenAsync()
        {
            Reads++;
            return Fail ? ValueTask.FromException<string?>(new InvalidOperationException("secret-test-credential"))
                : ReadToken?.Invoke() ?? ValueTask.FromResult<string?>(Tokens.Token);
        }
        public Task ClearCacheAsync() { Clears++; return Task.CompletedTask; }
        public ValueTask<DateTime> GetAuthTokenExpiryDate() => throw new NotSupportedException();
        public ValueTask<DateTime> GetRefreshTokenExpiryDate() => throw new NotSupportedException();
        public ValueTask CacheAuthTokens(TokenResponse tokenResponse) { Saves++; Tokens = tokenResponse; return ValueTask.CompletedTask; }
        public ValueTask<string?> GetCachedRefreshTokenAsync() => ValueTask.FromResult<string?>(Tokens.RefreshToken);
        public ValueTask<ICollection<string>?> GetCachedPermissionsAsync() => throw new NotSupportedException();
    }

    private sealed class Authentication : IAuthenticationService
    {
        public event Action<string?>? LoginChange;
        public int SubscriberCount => LoginChange?.GetInvocationList().Length ?? 0;
        public int Logouts { get; private set; }
        public TwoFactorChallengeResponse? PendingChallenge => null;
        public string? ErrorMessage => null;
        public void RaiseLoginChange() => LoginChange?.Invoke("test");
        public Task<bool> LogoutAsync(string methodName) { Logouts++; return Task.FromResult(true); }
        public Task<bool> RefreshAsync() => throw new NotSupportedException("The test uses a fresh credential.");
        public Task<bool> LoginAsync(TokenRequest model) => throw new NotSupportedException();
        public Task<bool> VerifyTwoFactorAsync(string code, bool recovery) => throw new NotSupportedException();
        public void CancelTwoFactor() => throw new NotSupportedException();
    }

    internal sealed class TokenApi : IAuthApiService
    {
        public Func<CancellationToken, Task<Response<TokenResponse>>> Refresh { get; set; } = _ => throw new NotSupportedException("The test uses a fresh credential.");
        public int Refreshes { get; private set; }
        public int Logouts { get; private set; }
        public Task<Response<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellation)
        { Refreshes++; return Refresh(cancellation); }
        public Task LogoutAsync(CancellationToken cancellation) { Logouts++; return Task.CompletedTask; }
        public Task<Response<TokenResponse>> CompleteTwoFactorAsync(TwoFactorRequest request, CancellationToken cancellation) => throw new NotSupportedException();
        public Task<Response<LoginResponse>> CreateTokenAsync(TokenRequest request, CancellationToken cancellation) => throw new NotSupportedException();
        public Task<Response<LoginResponse>> ExternalAuthAsync(TokenRequest request, CancellationToken cancellation) => throw new NotSupportedException();
        public Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellation) => throw new NotSupportedException();
    }

    internal sealed class FormFactor(string name = "WebAssembly") : IFormFactor
    {
        public string GetFormFactor() => name;
        public string GetPlatform() => "test";
    }

    private sealed class ComponentActivator(IServiceProvider services) : IComponentActivator
    {
        public IComponent CreateInstance(Type componentType) => (IComponent)ActivatorUtilities.CreateInstance(services, componentType);
    }

    private sealed record Failure(string Message, Exception? Exception);

    private sealed class FailureLogger : ILogger<SignalRService>
    {
        public Channel<Failure> Failures { get; } = Channel.CreateUnbounded<Failure>();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning) Failures.Writer.TryWrite(new Failure(formatter(state, exception), exception));
        }
    }
}
