using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;

public class TenantIdentifier(IServiceProvider serviceProvider, IConfiguration configuration)
{
    public (string tenantIdentifier, string backendUrl, string rootDomain, string clientBaseAddress, bool isServerSide) Get()
    {
        IFormFactor formFactor = serviceProvider.GetRequiredService<IFormFactor>();
        string navigationManagerBaseUri;
        string tenantIdentifier;
        string formFactorType = formFactor.GetFormFactor();
        bool isServerSide = formFactorType == "Web";

        if (formFactorType == "Web")
        {
            IHttpContextAccessor httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
            HttpRequest? httpRequest = httpContextAccessor.HttpContext?.Request;
            if (httpRequest == null)
            {
                throw new Exception("Request is null");
            }

            navigationManagerBaseUri = $"{httpRequest.Scheme}://{httpRequest.Host}";
        }
        else if (formFactorType == "WebAssembly")
        {
            NavigationManager navigationManager = serviceProvider.GetRequiredService<NavigationManager>();
            navigationManagerBaseUri = navigationManager.BaseUri;
        }
        else
        {
            navigationManagerBaseUri = "https://krafter.getkrafter.dev";
        }

        var uri = new Uri(navigationManagerBaseUri);
        string host = uri.Host;
        string backendUrl;
        bool isRunningLocally = host == "localhost" || host == "127.0.0.1";
        string clientBaseAddress;

        string remoteHostUrl = configuration["RemoteHostUrl"] ?? throw new InvalidOperationException("RemoteHostUrl not configured");
        bool isRemoteHostFullUrl = remoteHostUrl.StartsWith("http://") || remoteHostUrl.StartsWith("https://");

        if (isRunningLocally || isRemoteHostFullUrl)
        {
            // Local dev or server-side (Aspire provides full URL like https://localhost:5199)
            tenantIdentifier = isRunningLocally ? "krafter" : (host.Split('.').Length > 2 ? host.Split('.')[0] : "krafter");
            clientBaseAddress = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
            backendUrl = remoteHostUrl;
        }
        else
        {
            // Production WASM: RemoteHostUrl is domain only (e.g., api.getkrafter.dev)
            // AditiKraft.Krafter.Backend URL is built as: https://{tenant}.{RemoteHostUrl}
            string[] strings = host.Split('.');
            tenantIdentifier = strings.Length > 2 ? strings[0] : "api";
            backendUrl = $"https://{tenantIdentifier}.{remoteHostUrl}";
            clientBaseAddress = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        }

        string prefix = tenantIdentifier + ".";
        string rootDomain = host.StartsWith(prefix) ? host.Substring(prefix.Length) : host;
        return (tenantIdentifier, backendUrl, rootDomain, clientBaseAddress, isServerSide);
    }
}
