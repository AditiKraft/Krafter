using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Common.Interfaces.Auth;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Common.Extensions;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Enums;
using AditiKraft.Krafter.Contracts.Common.Models;
using Mapster;

namespace AditiKraft.Krafter.Backend.Web.Middleware;

public class MultiTenantServiceMiddleware(
    ITenantFinderService tenantFinderService,
    ITenantSetterService tenantSetterService,
    ICurrentUser currentUser) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (TenantSettings.TenancyMode == TenancyMode.Single)
        {
            Tenant tenant = KrafterInitialConstants.KrafterTenant;
            CurrentTenantDetails currentTenantDetails = tenant.Adapt<CurrentTenantDetails>();
            currentTenantDetails.TenantLink = context.Request.GetOrigin();
            currentTenantDetails.IpAddress = context.Connection?.RemoteIpAddress?.ToString();
            currentTenantDetails.UserId = currentUser.GetUserId();
            currentTenantDetails.Host = $"https://{context.Request.Host.Value}";
            tenantSetterService.SetTenant(currentTenantDetails);

            await next(context);
            return;
        }

        string? tenantIdentifier = "";
        string host = context.Request.Host.Value ?? "";
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

        Response<Tenant> tenantResponse = await tenantFinderService.Find(tenantIdentifier);
        if (tenantResponse.IsError || tenantResponse.Data is null)
        {
            context.Response.StatusCode = tenantResponse.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = tenantResponse.Message });
            return;
        }

        Tenant tenantResult = tenantResponse.Data;
        CurrentTenantDetails currentTenant = tenantResult.Adapt<CurrentTenantDetails>();
        currentTenant.TenantLink = context.Request.GetOrigin();
        currentTenant.IpAddress = context.Connection?.RemoteIpAddress?.ToString();
        currentTenant.UserId = currentUser.GetUserId();
        currentTenant.Host = $"https://{context.Request.Host.Value}";
        tenantSetterService.SetTenant(currentTenant);
        await next(context);
    }
}
