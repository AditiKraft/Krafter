using Krafter.Api.Client;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Features.Auth._Shared;
using Krafter.UI.Web.Client.Infrastructure.Services;
using Krafter.UI.Web.Client.Infrastructure.Storage;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace Krafter.UI.Web.Client.Kiota
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddKrafterKiotaClient(this IServiceCollection services, string baseUrl)
        {
            // Local storage service for tokens
          //  services.AddScoped<IKrafterLocalStorageService, KrafterLocalStorageService>();

            // Token provider
            services.AddScoped<IAccessTokenProvider>(sp =>
            {
                var localStorage = sp.GetRequiredService<IKrafterLocalStorageService>();
                var authenticationService = sp.GetRequiredService<IAuthenticationService>();
                return new RefreshingTokenProvider(localStorage, authenticationService);
            });

            // Auth provider
            services.AddScoped<IAuthenticationProvider>(sp =>
            {
                var tokenProvider = sp.GetRequiredService<IAccessTokenProvider>();
                return new BaseBearerTokenAuthenticationProvider(tokenProvider);
            });

            // KrafterClient with tenant + culture headers
            services.AddScoped(sp =>
            {
                var authProvider = sp.GetRequiredService<IAuthenticationProvider>();
                var tenantIdentifierProvider
                    = sp.GetRequiredService<TenantIdentifier>();
                var tenantIdentifierProviderResult = tenantIdentifierProvider.Get();
                var tenantHandler = new TenantHeaderHandler(tenantIdentifierProviderResult)
                {
                    InnerHandler = new HttpClientHandler()
                };
                var httpClient = new HttpClient(tenantHandler);

                var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient)
                {
                    BaseUrl = tenantIdentifierProviderResult.hostUrl
                };

                return new KrafterClient(adapter);
            });

            return services;
        }
    }
}
