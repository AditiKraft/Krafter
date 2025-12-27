using System.Globalization;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Infrastructure.Http;

namespace Krafter.UI.Web.Client.Infrastructure.Refit;

/// <summary>
/// DelegatingHandler that injects tenant identifier and culture headers for Refit clients.
/// </summary>
public class RefitTenantHandler(TenantIdentifier tenantIdentifier) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var tenantInfo = tenantIdentifier.Get();

        // Set static TenantInfo for backward compatibility
        TenantInfo.Identifier = tenantInfo.tenantIdentifier;
        TenantInfo.HostUrl = tenantInfo.remoteHostUrl;
        TenantInfo.MainDomain = tenantInfo.rootDomain;

        // Inject tenant header
        request.Headers.Remove("x-tenant-identifier");
        request.Headers.Add("x-tenant-identifier", tenantInfo.tenantIdentifier);

        // Inject culture header
        request.Headers.AcceptLanguage.Clear();
        request.Headers.AcceptLanguage.ParseAdd(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

        // Ensure base address is set for relative URIs
        if (request.RequestUri != null && !request.RequestUri.IsAbsoluteUri)
        {
            request.RequestUri = new Uri(new Uri(tenantInfo.remoteHostUrl), request.RequestUri);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
