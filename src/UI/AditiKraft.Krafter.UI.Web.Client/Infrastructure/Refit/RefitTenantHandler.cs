using System.Globalization;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;
using Microsoft.AspNetCore.Http;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

public class RefitTenantHandler(TenantIdentifier tenantIdentifier, bool isBffClient = false,
    IHttpContextAccessor? httpContextAccessor = null) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        (string tenantIdentifier, string backendUrl, string rootDomain, string clientBaseAddress, bool isServerSide)
            tenantInfo =
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

        if (tenantInfo.isServerSide)
        {
            // Forward only the connection resolved by trusted proxy middleware, never incoming header text.
            request.Headers.Remove("X-Forwarded-For");
            request.Headers.Remove("X-Forwarded-Proto");
            HttpContext? context = httpContextAccessor?.HttpContext;
            if (context?.Connection.RemoteIpAddress is { } address)
            {
                request.Headers.TryAddWithoutValidation("X-Forwarded-For", address.ToString());
                request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", context.Request.Scheme);
            }
        }

        // Rewrite URL based on client type and tenant
        // Server-side: Always use backendUrl (no BFF needed, server manages cookies directly)
        // WASM: BFF clients use clientBaseAddress (for cookie management), others use backendUrl
        if (request.RequestUri != null)
        {
            string targetBaseUrl = tenantInfo.isServerSide
                ? tenantInfo.backendUrl
                : isBffClient
                    ? tenantInfo.clientBaseAddress
                    : tenantInfo.backendUrl;

            // Get the path and query from the original request
            string pathAndQuery = request.RequestUri.IsAbsoluteUri
                ? request.RequestUri.PathAndQuery
                : request.RequestUri.ToString();

            request.RequestUri = new Uri(new Uri(targetBaseUrl), pathAndQuery);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
