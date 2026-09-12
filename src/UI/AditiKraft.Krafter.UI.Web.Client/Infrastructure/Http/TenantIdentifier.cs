using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Enums;
using Microsoft.AspNetCore.Http;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;

public class TenantIdentifier(IServiceProvider serviceProvider, AppUrls urls)
{
    public (string tenantIdentifier, string backendUrl, string rootDomain, string clientBaseAddress, bool isServerSide)
        Get()
    {
        string formFactor = serviceProvider.GetRequiredService<IFormFactor>().GetFormFactor();
        bool isServerSide = formFactor == "Web";
        Uri uiUri = GetCurrentUiUri(formFactor);
        if (TenantSettings.TenancyMode == TenancyMode.Multi && urls.IsInvalidUiTenantHost(uiUri.Host))
        {
            throw new InvalidOperationException("Tenant not found.");
        }
        string? subdomain = TenantSettings.TenancyMode == TenancyMode.Multi
            ? urls.GetUiTenantIdentifier(uiUri.Host)
            : null;
        string tenantIdentifier = subdomain ?? DefaultTenantConstants.Identifier;
        string clientBaseAddress = uiUri.GetLeftPart(UriPartial.Authority);
        Uri apiUri;

        if (isServerSide)
        {
            // Server requests use the configured connection address and carry the tenant in a header.
            apiUri = urls.GetServerApiUri();
        }
        else
        {
            apiUri = urls.GetApiUri() ?? uiUri;
        }

        string rootDomain = urls.GetTenantBaseUri().Host;
        return (tenantIdentifier, apiUri.GetLeftPart(UriPartial.Authority), rootDomain, clientBaseAddress, isServerSide);
    }

    private Uri GetCurrentUiUri(string formFactor)
    {
        if (formFactor == "Web")
        {
            HttpRequest request = serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext?.Request
                ?? throw new InvalidOperationException("The current HTTP request is required to resolve tenant URLs.");
            return new Uri($"{request.Scheme}://{request.Host}");
        }

        if (formFactor == "WebAssembly")
        {
            return new Uri(serviceProvider.GetRequiredService<NavigationManager>().BaseUri);
        }

        return urls.GetRootUiUri();
    }

}
