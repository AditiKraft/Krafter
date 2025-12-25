using System.Text.RegularExpressions;
using Backend.Application.Common;
using Backend.Common.Extensions;
using Backend.Common.Interfaces;
using Backend.Common.Interfaces.Auth;
using Backend.Common.Models;
using Backend.Features.Tenants._Shared;
using Backend.Features.Users._Shared;
using Mapster;
using Microsoft.AspNetCore.SignalR;

namespace Backend.Hubs;

public class RealtimeHub(ILogger<RealtimeHub> logger) : Hub
{
    public async Task SendMessage(string user, string message) =>
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
            CurrentTenantDetails? res = await SetTenantContextAsync(httpContext, Context, tenantFinderService,
                tenantSetterService, currentUser);
            if (res is null)
            {
                throw new UnauthorizedException("Authentication Failed.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"GroupTenant-{res.Id}");
        }
        else
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        await base.OnConnectedAsync();

        logger.LogInformation("A client connected to NotificationHub: {connectionId}", Context.ConnectionId);
    }


    private async Task<CurrentTenantDetails> SetTenantContextAsync(
        HttpContext httpContext,
        HubCallerContext context,
        ITenantFinderService tenantFinderService,
        ITenantSetterService tenantSetterService,
        ICurrentUser currentUser)
    {
        string tenantIdentifier = GetTenantIdentifier(httpContext);
        Tenant tenant = await tenantFinderService.Find(tenantIdentifier);
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
        string host = httpContext.Request.Host.Value;
        string pattern = @"^(.+)\.api\..*$";
        Match match = Regex.Match(host, pattern);

        if (match.Success)
        {
            tenantIdentifier = match.Groups[1].Value;
        }
        else
        {
            tenantIdentifier = httpContext.Request.Headers["x-tenant-identifier"];
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
            CurrentTenantDetails? res = await SetTenantContextAsync(httpContext, Context, tenantFinderService,
                tenantSetterService, currentUser);
            if (res is null)
            {
                throw new UnauthorizedException("Authentication Failed.");
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"GroupTenant-{res.Id}");
        }
        else
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        await base.OnConnectedAsync();

        logger.LogInformation("A client connected to NotificationHub: {connectionId}", Context.ConnectionId);


        await base.OnDisconnectedAsync(exception);

        logger.LogInformation("A client disconnected from NotificationHub: {connectionId}", Context.ConnectionId);
    }
}
