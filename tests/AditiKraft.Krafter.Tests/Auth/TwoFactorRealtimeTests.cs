using System.Security.Claims;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Infrastructure.Realtime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class TwoFactorRealtimeTests
{
    [Fact]
    public async Task RevokedSessionCannotInvokeAnExistingHubConnection()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        TenantDbContext tenants = scope.Services.GetRequiredService<TenantDbContext>();
        tenants.Tenants.Add(new Tenant { Id = "test", Identifier = "test", Name = "Test", AdminEmail = "owner@example.com", IsActive = true, ValidUpto = DateTime.MaxValue });
        await tenants.SaveChangesAsync();
        var user = await scope.AddUserAsync();
        var login = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        var http = new DefaultHttpContext { RequestServices = scope.Services };
        var context = new Caller(http, MfaHarness.Principal(login.Data!.Tokens!));
        var filter = new SessionHubFilter(harness.Services.GetRequiredService<IServiceScopeFactory>(), new Lifetime(), NullLogger<SessionHubFilter>.Instance);
        var hub = new RealtimeHub(NullLogger<RealtimeHub>.Instance) { Context = context };
        var lifetime = new HubLifetimeContext(context, scope.Services, hub);
        bool connected = false;
        await filter.OnConnectedAsync(lifetime, _ => { connected = true; return Task.CompletedTask; });
        Assert.True(connected);
        await harness.EnrollAsync(scope, user);
        var invocation = new HubInvocationContext(context, scope.Services, hub,
            typeof(RealtimeHub).GetMethod(nameof(RealtimeHub.SendMessageAsync))!, new object?[] { "user", "message" });
        await Assert.ThrowsAsync<HubException>(async () => await filter.InvokeMethodAsync(invocation, _ => ValueTask.FromResult<object?>(null)));
        Assert.True(context.Aborted);
        await filter.OnDisconnectedAsync(lifetime, null, (_, _) => Task.CompletedTask);
    }

    private sealed class Lifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
    private sealed class Caller : HubCallerContext
    {
        private readonly CancellationTokenSource cancellation = new();
        public Caller(HttpContext http, ClaimsPrincipal user)
        { User = user; Features.Set<IHttpContextFeature>(new HttpFeature { HttpContext = http }); }
        public bool Aborted { get; private set; }
        public override string ConnectionId => "mfa-test";
        public override string? UserIdentifier => "user";
        public override ClaimsPrincipal? User { get; }
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => cancellation.Token;
        public override void Abort() { Aborted = true; cancellation.Cancel(); }
    }
    private sealed class HttpFeature : IHttpContextFeature { public HttpContext? HttpContext { get; set; } }
}
