using System.Globalization;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Krafter.UI.Web.Client.Kiota
{
    public class TenantHeaderHandler(
        (string tenantIdentifier, string hostUrl, string host) tenantIdentifierProviderResult)
        : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            TenantInfo.Identifier = tenantIdentifierProviderResult.tenantIdentifier;
            TenantInfo.HostUrl= tenantIdentifierProviderResult.hostUrl;
            TenantInfo.MainDomain = tenantIdentifierProviderResult.host;
            // Inject headers
            request.Headers.Remove("x-tenant-identifier");
            request.Headers.Add("x-tenant-identifier", TenantInfo.Identifier);
            request.Headers.AcceptLanguage.Clear();
            request.Headers.AcceptLanguage.ParseAdd(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            // Ensure base address is set
            if (request.RequestUri != null && !request.RequestUri.IsAbsoluteUri)
            {
                request.RequestUri = new Uri(new Uri(TenantInfo.HostUrl), request.RequestUri);
            }
            return base.SendAsync(request, cancellationToken);
        }
    }

}
