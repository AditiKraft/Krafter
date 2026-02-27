using System.Text.RegularExpressions;
using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Common.Interfaces.Auth;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Common.Extensions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Hubs;
using Mapster;
using Microsoft.AspNetCore.SignalR;

namespace AditiKraft.Krafter.Backend.Infrastructure.Realtime;

public class RealtimeHub(ILogger<RealtimeHub> logger) : Hub
{
    private const string AuthenticationFailedMessage = "Authentication Failed.";

    public async Task SendMessageAsync(string user, string message) =>
        await Clients.All.SendAsync(nameof(SignalRMethods.ReceiveMessage), user, message);

    public override async Task OnConnectedAsync()
    {
        HttpContext? httpContext = Context.GetHttpContext();
        if (httpContext != null)
        {
            ITenantFinderService tenantFinderService =
                httpContext.RequestServices.GetRequiredService<ITenantFinderService>();
            ITenantSetterService tenantSetterService =
                httpContext.RequestServices.GetRequiredService<ITenantSetterService>();
            ICurrentUser currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUser>();
            CurrentTenantDetails? res = await SetTenantContextAsync(httpContext, tenantFinderService,
                tenantSetterService, currentUser);
            if (res is not {})
            {
                throw new HubException(AuthenticationFailedMessage);
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"GroupTenant-{res.Id}");
        }
        else
        {
            throw new HubException(AuthenticationFailedMessage);
        }

        await base.OnConnectedAsync();

        logger.LogInformation("A client connected to NotificationHub: {connectionId}", Context.ConnectionId);
    }


    private async Task<CurrentTenantDetails?> SetTenantContextAsync(
        HttpContext httpContext,
        ITenantFinderService tenantFinderService,
        ITenantSetterService tenantSetterService,
        ICurrentUser currentUser)
    {
        string tenantIdentifier = GetTenantIdentifier(httpContext);
        Response<Tenant> tenantResponse = await tenantFinderService.Find(tenantIdentifier);
        if (tenantResponse.IsError || tenantResponse.Data is null)
        {
            return null;
        }

        Tenant tenant = tenantResponse.Data;
        CurrentTenantDetails currentTenantDetails = tenant.Adapt<CurrentTenantDetails>();
        currentTenantDetails.TenantLink = httpContext.Request.GetOrigin();
        currentTenantDetails.IpAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        currentTenantDetails.UserId = currentUser.GetUserId();

        tenantSetterService.SetTenant(currentTenantDetails);
        return currentTenantDetails;
    }

    private string GetTenantIdentifier(HttpContext httpContext)
    {
        string tenantIdentifier = "";
        string host = httpContext.Request.Host.Value ?? "";
        string pattern = @"^(.+)\.api\..*$";
        Match match = Regex.Match(host, pattern);

        if (match.Success)
        {
            tenantIdentifier = match.Groups[1].Value;
        }
        else
        {
            tenantIdentifier = httpContext?.Request?.Headers["x-tenant-identifier"].ToString() ?? "";
        }

        if (string.IsNullOrWhiteSpace(tenantIdentifier))
        {
            tenantIdentifier = KrafterInitialConstants.RootTenant.Identifier;
        }

        return tenantIdentifier;
    }


    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        HttpContext? httpContext = Context.GetHttpContext();
        if (httpContext != null)
        {
            ITenantFinderService tenantFinderService =
                httpContext.RequestServices.GetRequiredService<ITenantFinderService>();
            ITenantSetterService tenantSetterService =
                httpContext.RequestServices.GetRequiredService<ITenantSetterService>();
            ICurrentUser currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUser>();
            CurrentTenantDetails? res = await SetTenantContextAsync(httpContext, tenantFinderService,
                tenantSetterService, currentUser);
            if (res is null)
            {
                throw new HubException(AuthenticationFailedMessage);
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"GroupTenant-{res.Id}");
        }
        else
        {
            throw new HubException(AuthenticationFailedMessage);
        }

        await base.OnConnectedAsync();

        logger.LogInformation("A client connected to NotificationHub: {connectionId}", Context.ConnectionId);


        await base.OnDisconnectedAsync(exception);

        logger.LogInformation("A client disconnected from NotificationHub: {connectionId}", Context.ConnectionId);
    }
}



