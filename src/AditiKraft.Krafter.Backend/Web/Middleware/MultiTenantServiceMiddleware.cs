using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Extensions;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Enums;
using AditiKraft.Krafter.Contracts.Common.Models;
using Mapster;

namespace AditiKraft.Krafter.Backend.Web.Middleware;

public class MultiTenantServiceMiddleware(
    ITenantFinderService tenantFinderService,
    ITenantSetterService tenantSetterService,
    ICurrentUser currentUser,
    IConfiguration configuration) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (TenantSettings.TenancyMode == TenancyMode.Single)
        {
            Tenant tenant = SeedDataConstants.DefaultTenant;
            CurrentTenantDetails currentTenantDetails = tenant.Adapt<CurrentTenantDetails>();
            currentTenantDetails.TenantLink = context.Request.GetOrigin();
            currentTenantDetails.IpAddress = context.Connection?.RemoteIpAddress?.ToString();
            currentTenantDetails.UserId = currentUser.GetUserId();
            currentTenantDetails.Host = $"https://{context.Request.Host.Value}";
            tenantSetterService.SetTenant(currentTenantDetails);

            await next(context);
            return;
        }

        string? tenantIdentifier = GetHostTenant(context.Request.Host.Host)
            ?? context.Request.Headers["x-tenant-identifier"].ToString();

        if (string.IsNullOrWhiteSpace(tenantIdentifier))
        {
            tenantIdentifier = SeedDataConstants.RootTenant.Identifier;
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

    private string? GetHostTenant(string host)
    {
        AppUrls urls = configuration.GetSection(AppUrls.SectionName).Get<AppUrls>() ?? new AppUrls();
        Uri rootUiUri = urls.GetRootUiUri();
        Uri? apiUri = urls.GetApiUri();
        Uri serverApiUri = urls.GetServerApiUri();
        if (host.Equals(rootUiUri.Host, StringComparison.OrdinalIgnoreCase) ||
            (apiUri is not null && host.Equals(apiUri.Host, StringComparison.OrdinalIgnoreCase)) ||
            host.Equals(serverApiUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return (apiUri is null ? null : AppUrls.GetSubdomain(host, apiUri))
            ?? AppUrls.GetSubdomain(host, rootUiUri);
    }

}
