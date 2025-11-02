using System.Globalization;
using Krafter.UI.Web.Client.Common.Models;

namespace Krafter.UI.Web.Client.Infrastructure.Http
{
    public static class HttpClientTenantConfigurator
    {
        public static void SetAPITenantHttpClientDefaults(IServiceCollection service, HttpClient client)
        {
           
            var serviceProvider = service.BuildServiceProvider();

            var tenantIdentifierProvider
                = serviceProvider.GetRequiredService<TenantIdentifier>();
            var tenantInformation = tenantIdentifierProvider.Get();
           

            TenantInfo.Identifier = tenantInformation.tenantIdentifier;
            TenantInfo.HostUrl = tenantInformation.remoteHostUrl;
            TenantInfo.MainDomain = tenantInformation.rootDomain;

            client.DefaultRequestHeaders.AcceptLanguage.Clear();
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(CultureInfo.DefaultThreadCurrentCulture?.TwoLetterISOLanguageName);
            client.BaseAddress = new Uri(TenantInfo.HostUrl);
            client.DefaultRequestHeaders.Add("x-tenant-identifier", TenantInfo.Identifier);
        }

        public static void SetBFFTenantHttpClientDefaults(IServiceCollection service, string remoteHostUrl, HttpClient client)
        {
            
            var serviceProvider = service.BuildServiceProvider();

            var tenantIdentifierProvider
                = serviceProvider.GetRequiredService<TenantIdentifier>();
            var tenantInformation = tenantIdentifierProvider.Get();

            TenantInfo.Identifier = tenantInformation.tenantIdentifier;
            TenantInfo.HostUrl = tenantInformation.remoteHostUrl;
            TenantInfo.MainDomain = tenantInformation.rootDomain;

            client.DefaultRequestHeaders.AcceptLanguage.Clear();
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(CultureInfo.DefaultThreadCurrentCulture?.TwoLetterISOLanguageName);
            client.BaseAddress = new Uri(tenantInformation.clientBaseAddress);
            client.DefaultRequestHeaders.Add("x-tenant-identifier", tenantInformation.tenantIdentifier);
        }
    }
}