using Backend.Common.Extensions;
using Backend.Common.Interfaces;
using Backend.Common.Interfaces.Auth;
using Backend.Common.Models;
using Backend.Features.Auth;
using Backend.Features.Tenants;
using Backend.Features.Users._Shared;
using Mapster;

namespace Backend.Api.Middleware;

public class MultiTenantServiceMiddleware(
    ITenantFinderService tenantFinderService,
    ITenantSetterService tenantSetterService,
    ICurrentUser currentUser) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        string? tenantIdentifier = "";
        string host = context.Request.Host.Value; // Get the host value from the HttpContext
        string[] strings = host.Split('.');
        if (strings.Length > 2)
        {
            tenantIdentifier = strings[0];
        }
        else
        {
            tenantIdentifier = context.Request.Headers["x-tenant-identifier"];
        }

        if (string.IsNullOrWhiteSpace(tenantIdentifier))
        {
            tenantIdentifier = KrafterInitialConstants.RootTenant.Identifier;
        }

        Tenant tenant = await tenantFinderService.Find(tenantIdentifier);
        CurrentTenantDetails currentTenantDetails = tenant.Adapt<CurrentTenantDetails>();
        currentTenantDetails.TenantLink = context.Request.GetOrigin();
        currentTenantDetails.IpAddress = context.Connection?.RemoteIpAddress?.ToString();
        currentTenantDetails.UserId = currentUser.GetUserId();
        currentTenantDetails.Host = $"https://{context.Request.Host.Value}";
        tenantSetterService.SetTenant(currentTenantDetails);
        await next(context);
    }
}
