using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Common.Interfaces.Auth;
using AditiKraft.Krafter.Backend.Features.Tenants._Shared;
using AditiKraft.Krafter.Backend.Features.Users._Shared;
using AditiKraft.Krafter.Backend.Common.Extensions;
using AditiKraft.Krafter.Backend.Features.Auth;
using AditiKraft.Krafter.Backend.Features.Tenants;
using AditiKraft.Krafter.Contracts.Common.Models;
using Mapster;

namespace AditiKraft.Krafter.Backend.Api.Middleware;

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

        Response<Tenant> tenantResponse = await tenantFinderService.Find(tenantIdentifier);
        if (tenantResponse.IsError || tenantResponse.Data is null)
        {
            context.Response.StatusCode = tenantResponse.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = tenantResponse.Message });
            return;
        }

        Tenant tenant = tenantResponse.Data;
        CurrentTenantDetails currentTenantDetails = tenant.Adapt<CurrentTenantDetails>();
        currentTenantDetails.TenantLink = context.Request.GetOrigin();
        currentTenantDetails.IpAddress = context.Connection?.RemoteIpAddress?.ToString();
        currentTenantDetails.UserId = currentUser.GetUserId();
        currentTenantDetails.Host = $"https://{context.Request.Host.Value}";
        tenantSetterService.SetTenant(currentTenantDetails);
        await next(context);
    }
}
