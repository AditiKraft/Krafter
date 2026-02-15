using System.Globalization;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

public class RefitTenantHandler(TenantIdentifier tenantIdentifier, bool isBffClient = false) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        (string tenantIdentifier, string backendUrl, string rootDomain, string clientBaseAddress, bool isServerSide) tenantInfo =
            tenantIdentifier.Get();

        // Set static TenantInfo for backward compatibility
        TenantInfo.Identifier = tenantInfo.tenantIdentifier;
        TenantInfo.HostUrl = tenantInfo.backendUrl;
        TenantInfo.MainDomain = tenantInfo.rootDomain;

        // Inject tenant header
        request.Headers.Remove("x-tenant-identifier");
        request.Headers.Add("x-tenant-identifier", tenantInfo.tenantIdentifier);

        // Inject culture header
        request.Headers.AcceptLanguage.Clear();
        request.Headers.AcceptLanguage.ParseAdd(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

        // Rewrite URL based on client type and tenant
        // Server-side: Always use backendUrl (no BFF needed, server manages cookies directly)
        // WASM: BFF clients use clientBaseAddress (for cookie management), others use backendUrl
        if (request.RequestUri != null)
        {
            string targetBaseUrl = tenantInfo.isServerSide 
                ? tenantInfo.backendUrl 
                : (isBffClient ? tenantInfo.clientBaseAddress : tenantInfo.backendUrl);
            
            // Get the path and query from the original request
            string pathAndQuery = request.RequestUri.IsAbsoluteUri 
                ? request.RequestUri.PathAndQuery 
                : request.RequestUri.ToString();
            
            request.RequestUri = new Uri(new Uri(targetBaseUrl), pathAndQuery);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
