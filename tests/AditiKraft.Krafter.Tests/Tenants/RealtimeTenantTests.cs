using System.Security.Claims;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Infrastructure.Realtime;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Realtime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AditiKraft.Krafter.Tests.Tenants;

public sealed class RealtimeTenantTests
{
    [Theory]
    [InlineData("api.example.com")]
    [InlineData("blue.example.com")]
    [InlineData("localhost:7291")]
    public async Task ConnectionAndMessagesUseMiddlewareTenantAcrossHostingModes(string host)
    {
        var tenantService = new CurrentTenantService();
        tenantService.SetTenant(new CurrentTenantDetails
        {
            Id = "blue-id", Identifier = "blue", TenantLink = "https://blue.example.com"
        });
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton<ITenantGetterService>(tenantService).BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Host = new HostString(host);
        // A hub must not reinterpret the request after middleware has selected the tenant.
        httpContext.Request.Headers["x-tenant-identifier"] = "red";
        httpContext.Request.QueryString = new QueryString("?tenantIdentifier=red");
        var groups = new RecordingGroupManager();
        var clients = new RecordingClients();
        var hub = new RealtimeHub(NullLogger<RealtimeHub>.Instance)
        {
            Context = new TestHubContext(httpContext), Groups = groups, Clients = clients
        };

        await hub.OnConnectedAsync();
        await hub.SendMessageAsync("user", "message");

        Assert.Equal("GroupTenant-blue-id", groups.AddedGroup);
        Assert.Equal("GroupTenant-blue-id", clients.SelectedGroup);
        Assert.Equal(nameof(SignalRMethods.ReceiveMessage), clients.Proxy.Method);
        Assert.Equal(new object?[] { "user", "message" }, clients.Proxy.Arguments);

        tenantService.SetTenant(new CurrentTenantDetails
        {
            Id = "red-id", Identifier = "red", TenantLink = "https://red.example.com"
        });
        await hub.OnDisconnectedAsync(null);
        Assert.Equal("GroupTenant-blue-id", groups.RemovedGroup);
    }

    [Fact]
    public async Task MessagesRequireAnEstablishedTenantGroup()
    {
        var hub = new RealtimeHub(NullLogger<RealtimeHub>.Instance)
        {
            Context = new TestHubContext(new DefaultHttpContext()), Clients = new RecordingClients()
        };

        await Assert.ThrowsAsync<HubException>(() => hub.SendMessageAsync("user", "message"));
    }

    private sealed class TestHubContext : HubCallerContext
    {
        public TestHubContext(HttpContext httpContext)
        {
            Features.Set<IHttpContextFeature>(new HttpContextFeature { HttpContext = httpContext });
        }

        public override string ConnectionId => "connection";
        public override string? UserIdentifier => "user";
        public override ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }

    private sealed class HttpContextFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public string? AddedGroup { get; private set; }
        public string? RemovedGroup { get; private set; }

        public Task AddToGroupAsync(string connectionId, string groupName,
            CancellationToken cancellationToken = default)
        {
            AddedGroup = groupName;
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName,
            CancellationToken cancellationToken = default)
        {
            RemovedGroup = groupName;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingClients : IHubCallerClients
    {
        public string? SelectedGroup { get; private set; }
        public RecordingClientProxy Proxy { get; } = new();

        public IClientProxy Group(string groupName)
        {
            SelectedGroup = groupName;
            return Proxy;
        }

        public IClientProxy All => throw new InvalidOperationException("Tenant messages must not be broadcast.");
        public IClientProxy Caller => throw new NotSupportedException();
        public IClientProxy Others => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy OthersInGroup(string groupName) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class RecordingClientProxy : IClientProxy
    {
        public string? Method { get; private set; }
        public object?[]? Arguments { get; private set; }

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Method = method;
            Arguments = args;
            return Task.CompletedTask;
        }
    }
}
