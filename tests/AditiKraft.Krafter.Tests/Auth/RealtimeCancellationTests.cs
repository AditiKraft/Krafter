using System.Collections.Concurrent;
using System.Security.Claims;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Realtime;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class RealtimeCancellationTests
{
    [Theory]
    [InlineData("connection", false)]
    [InlineData("connection", true)]
    [InlineData("invocation", false)]
    [InlineData("invocation", true)]
    [InlineData("monitor", false)]
    [InlineData("monitor", true)]
    public async Task LifecycleCancellationStopsPendingTenantValidationWithoutWarningAsync(string phase, bool stopping)
    {
        using var harness = new MfaHarness();
        using MfaScope seed = harness.Scope();
        var user = await seed.AddUserAsync();
        await RealtimePermissionTestData.GrantAsync(seed, user.Id);
        var login = await seed.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        var gate = new TenantLookupGate { Block = phase != "monitor" };
        var scopes = new TrackingScopeFactory(harness.Services.GetRequiredService<IServiceScopeFactory>(), gate);
        var logger = new RecordingLogger();
        using var lifetime = new Lifetime();
        var filter = new SessionHubFilter(scopes, lifetime, logger);
        var http = new DefaultHttpContext { RequestServices = seed.Services };
        http.Items[CurrentTenantService.ResolvedTenantItemKey] = seed.Services.GetRequiredService<ITenantGetterService>().Tenant;
        using var caller = new Caller(http, MfaHarness.Principal(login.Data!.Tokens!));
        var hub = new RealtimeHub(NullLogger<RealtimeHub>.Instance) { Context = caller };
        var connection = new HubLifetimeContext(caller, seed.Services, hub);
        bool continued = false;
        Task operation;
        if (phase == "invocation")
        {
            var invocation = new HubInvocationContext(caller, seed.Services, hub,
                typeof(RealtimeHub).GetMethod(nameof(RealtimeHub.SendMessageAsync))!, ["user", "message"]);
            operation = filter.InvokeMethodAsync(invocation, _ =>
            {
                continued = true;
                return ValueTask.FromResult<object?>(null);
            }).AsTask();
        }
        else
        {
            operation = filter.OnConnectedAsync(connection, _ =>
            {
                continued = true;
                return Task.CompletedTask;
            });
        }

        try
        {
            if (phase == "monitor")
            {
                await operation.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(continued);
                gate.Block = true;
            }

            // Wait until validation is in flight, including the real periodic monitor tick.
            await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (stopping)
            {
                lifetime.StopApplication();
            }
            else
            {
                caller.Abort();
            }

            await gate.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(2));
            if (phase == "invocation")
            {
                await Assert.ThrowsAsync<HubException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2)));
            }
            else
            {
                await operation.WaitAsync(TimeSpan.FromSeconds(2));
            }
            await filter.OnDisconnectedAsync(connection, null, (_, _) => Task.CompletedTask)
                .WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(phase == "monitor", continued);
            Assert.True(caller.Aborted);
            Assert.NotEmpty(scopes.Created);
            Assert.All(scopes.Created, scope => Assert.True(scope.Disposed));
            Assert.Empty(logger.Warnings);
        }
        finally
        {
            // Also release an uncancelable regression so a failed assertion cannot strand a task.
            gate.Release.TrySetResult();
            caller.Abort();
            lifetime.StopApplication();
            await Record.ExceptionAsync(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
            await filter.OnDisconnectedAsync(connection, null, (_, _) => Task.CompletedTask)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class TenantLookupGate : ITenantFinderService
    {
        public volatile bool Block;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<Response<Tenant>> Find(string? identifier, CancellationToken cancellationToken = default)
        {
            if (Block)
            {
                Started.TrySetResult();
                try
                {
                    await Release.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Canceled.TrySetResult();
                    throw;
                }
            }
            return Response<Tenant>.Success(new Tenant
            {
                Id = "test", Identifier = "test", Name = "Test", AdminEmail = "owner@example.test",
                IsActive = true, ValidUpto = DateTime.MaxValue
            });
        }
    }

    private sealed class TrackingScopeFactory(IServiceScopeFactory inner, ITenantFinderService finder) : IServiceScopeFactory
    {
        public ConcurrentBag<TrackingScope> Created { get; } = new();

        public IServiceScope CreateScope()
        {
            var scope = new TrackingScope(inner.CreateScope(), finder);
            Created.Add(scope);
            return scope;
        }
    }

    private sealed class TrackingScope(IServiceScope inner, ITenantFinderService finder) : IServiceScope, IServiceProvider
    {
        public bool Disposed { get; private set; }
        public IServiceProvider ServiceProvider => this;
        public object? GetService(Type serviceType) => serviceType == typeof(ITenantFinderService)
            ? finder : inner.ServiceProvider.GetService(serviceType);

        public void Dispose()
        {
            inner.Dispose();
            Disposed = true;
        }
    }

    private sealed class RecordingLogger : ILogger<SessionHubFilter>
    {
        public ConcurrentQueue<LogLevel> Warnings { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                Warnings.Enqueue(logLevel);
            }
        }
    }

    private sealed class Lifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource stopping = new();
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => stopping.Cancel();
        public void Dispose() => stopping.Dispose();
    }

    private sealed class Caller : HubCallerContext, IDisposable
    {
        private readonly CancellationTokenSource aborted = new();
        public Caller(HttpContext http, ClaimsPrincipal user)
        {
            User = user;
            Features.Set<IHttpContextFeature>(new HttpFeature { HttpContext = http });
        }
        public bool Aborted { get; private set; }
        public override string ConnectionId => "cancellation-test";
        public override string? UserIdentifier => "user";
        public override ClaimsPrincipal? User { get; }
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => aborted.Token;
        public override void Abort() { Aborted = true; aborted.Cancel(); }
        public void Dispose() => aborted.Dispose();
    }

    private sealed class HttpFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }
}
