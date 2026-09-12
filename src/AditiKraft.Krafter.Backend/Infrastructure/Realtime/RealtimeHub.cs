using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace AditiKraft.Krafter.Backend.Infrastructure.Realtime;

public class RealtimeHub(ILogger<RealtimeHub> logger) : Hub
{
    private const string AuthenticationFailedMessage = "Authentication Failed.";
    private const string TenantGroupKey = "RealtimeTenantGroup";

    public async Task SendMessageAsync(string user, string message)
    {
        if (!Context.Items.TryGetValue(TenantGroupKey, out object? value) || value is not string group)
        {
            throw new HubException(AuthenticationFailedMessage);
        }

        await Clients.Group(group).SendAsync(nameof(SignalRMethods.ReceiveMessage), user, message);
    }

    public override async Task OnConnectedAsync()
    {
        HttpContext? httpContext = Context.GetHttpContext();
        if (httpContext is null)
        {
            throw new HubException(AuthenticationFailedMessage);
        }

        // Hub instances have their own scope. Use the HTTP scope populated by tenant middleware.
        CurrentTenantDetails tenant = httpContext.RequestServices
            .GetRequiredService<ITenantGetterService>().Tenant;
        if (string.IsNullOrWhiteSpace(tenant.Id))
        {
            throw new HubException(AuthenticationFailedMessage);
        }

        string group = $"GroupTenant-{tenant.Id}";
        Context.Items[TenantGroupKey] = group;
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        await base.OnConnectedAsync();

        logger.LogInformation("A client connected to RealtimeHub: {ConnectionId}", Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(TenantGroupKey, out object? value) && value is string group)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        }

        await base.OnDisconnectedAsync(exception);
        logger.LogInformation("A client disconnected from RealtimeHub: {ConnectionId}", Context.ConnectionId);
    }
}
