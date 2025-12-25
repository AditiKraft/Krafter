using Krafter.Api.Client;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Features.Auth._Shared;
using Krafter.UI.Web.Client.Infrastructure.Http;
using Krafter.UI.Web.Client.Infrastructure.Services;
using Krafter.UI.Web.Client.Infrastructure.Storage;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace Krafter.UI.Web.Client.Kiota;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKrafterKiotaClient(this IServiceCollection services, string baseUrl)
    {
        // Local storage service for tokens
        //  services.AddScoped<IKrafterLocalStorageService, KrafterLocalStorageService>();

        // Token provider
        services.AddScoped<IAccessTokenProvider>(sp =>
        {
            IKrafterLocalStorageService localStorage = sp.GetRequiredService<IKrafterLocalStorageService>();
            IAuthenticationService authenticationService = sp.GetRequiredService<IAuthenticationService>();
            return new RefreshingTokenProvider(localStorage, authenticationService);
        });

        // Auth provider
        services.AddScoped<IAuthenticationProvider>(sp =>
        {
            IAccessTokenProvider tokenProvider = sp.GetRequiredService<IAccessTokenProvider>();
            return new BaseBearerTokenAuthenticationProvider(tokenProvider);
        });

        // KrafterClient with tenant + culture headers
        services.AddScoped(sp =>
        {
            IAuthenticationProvider authProvider = sp.GetRequiredService<IAuthenticationProvider>();
            TenantIdentifier tenantIdentifierProvider
                = sp.GetRequiredService<TenantIdentifier>();
            (string tenantIdentifier, string remoteHostUrl, string rootDomain, string clientBaseAddress)
                tenantIdentifierProviderResult = tenantIdentifierProvider.Get();
            var tenantHandler = new TenantHeaderHandler(tenantIdentifierProviderResult)
            {
                InnerHandler = new HttpClientHandler()
            };
            var httpClient = new HttpClient(tenantHandler);

            var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient)
            {
                BaseUrl = tenantIdentifierProviderResult.remoteHostUrl
            };

            return new KrafterClient(adapter);
        });

        return services;
    }
}
