using AditiKraft.Krafter.Backend.Common.Auth;
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
        AppUrls urls = configuration.GetSection(AppUrls.SectionName).Get<AppUrls>() ?? new AppUrls();
        if (TenantSettings.TenancyMode == TenancyMode.Single)
        {
            Tenant tenant = SeedDataConstants.DefaultTenant;
            CurrentTenantDetails currentTenantDetails = tenant.Adapt<CurrentTenantDetails>();
            currentTenantDetails.TenantLink = urls.GetRootUiUri().GetLeftPart(UriPartial.Authority);
            currentTenantDetails.IpAddress = context.Connection?.RemoteIpAddress?.ToString();
            currentTenantDetails.UserId = currentUser.GetUserId();
            currentTenantDetails.Host = $"https://{context.Request.Host.Value}";
            tenantSetterService.SetTenant(currentTenantDetails);

            await next(context);
            return;
        }

        string host = context.Request.Host.Host;
        bool isConnectionHost = IsConnectionHost(host, urls);
        if (!isConnectionHost && urls.IsInvalidUiTenantHost(host))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant not found" });
            return;
        }

        string? tenantIdentifier = isConnectionHost ? null : urls.GetUiTenantIdentifier(host);
        string header = context.Request.Headers["x-tenant-identifier"].ToString();
        tenantIdentifier ??= string.IsNullOrWhiteSpace(header) ? null : header;
        string hubPath = $"/{ApiRoutes.ApiPrefix}/RealtimeHub";
        if (tenantIdentifier is null &&
            (context.Request.Path.Equals(hubPath, StringComparison.OrdinalIgnoreCase) ||
             context.Request.Path.Equals(hubPath + "/negotiate", StringComparison.OrdinalIgnoreCase)))
        {
            tenantIdentifier = context.Request.Query["tenantIdentifier"].ToString();
        }

        if (string.IsNullOrWhiteSpace(tenantIdentifier))
        {
            tenantIdentifier = SeedDataConstants.RootTenant.Identifier;
        }

        if (!tenantIdentifier.Equals(DefaultTenantConstants.Identifier, StringComparison.OrdinalIgnoreCase) &&
            urls.IsReservedTenantIdentifier(tenantIdentifier))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant not found" });
            return;
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
        currentTenant.TenantLink = TenantLinkBuilder.GetTenantLink(urls, tenantResult.Identifier);
        currentTenant.IpAddress = context.Connection?.RemoteIpAddress?.ToString();
        currentTenant.UserId = currentUser.GetUserId();
        currentTenant.Host = $"https://{context.Request.Host.Value}";
        tenantSetterService.SetTenant(currentTenant);
        await next(context);
    }

    private static bool IsConnectionHost(string host, AppUrls urls)
    {
        Uri rootUiUri = urls.GetRootUiUri();
        Uri? apiUri = urls.GetApiUri();
        Uri serverApiUri = urls.GetServerApiUri();
        return host.Equals(rootUiUri.Host, StringComparison.OrdinalIgnoreCase) ||
            (apiUri is not null && host.Equals(apiUri.Host, StringComparison.OrdinalIgnoreCase)) ||
            host.Equals(serverApiUri.Host, StringComparison.OrdinalIgnoreCase);
    }

}
