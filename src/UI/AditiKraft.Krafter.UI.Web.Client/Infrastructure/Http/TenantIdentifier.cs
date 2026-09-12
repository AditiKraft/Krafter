using System.Net;
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
        Uri rootUiUri = urls.GetRootUiUri();
        string? subdomain = TenantSettings.TenancyMode == TenancyMode.Multi
            ? AppUrls.GetSubdomain(uiUri.Host, rootUiUri)
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
            Uri? publicApiUri = urls.GetApiUri();
            apiUri = publicApiUri ?? uiUri;
            if (publicApiUri is not null && subdomain is not null && CanAddTenantSubdomain(publicApiUri))
            {
                apiUri = new UriBuilder(publicApiUri) { Host = $"{subdomain}.{publicApiUri.Host}" }.Uri;
            }
        }

        string rootDomain = subdomain is null ? uiUri.Host : rootUiUri.Host;
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

    private static bool CanAddTenantSubdomain(Uri uri) =>
        uri.HostNameType == UriHostNameType.Dns &&
        !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
        !uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) &&
        !IPAddress.TryParse(uri.Host, out _);
}
