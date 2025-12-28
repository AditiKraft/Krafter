using Krafter.UI.Web.Client.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Krafter.UI.Web.Client.Infrastructure.Http;

public class TenantIdentifier(IServiceProvider serviceProvider, IConfiguration configuration)
{
    public (string tenantIdentifier, string backendUrl, string rootDomain, string clientBaseAddress) Get()
    {
        IFormFactor formFactor = serviceProvider.GetRequiredService<IFormFactor>();
        string navigationManagerBaseUri;
        string tenantIdentifier;
        string formFactorType = formFactor.GetFormFactor();

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

        if (isRunningLocally)
        {
            tenantIdentifier = "krafter"; // adjust if you want different local logic
            clientBaseAddress = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
            backendUrl = configuration["BackendUrl"] ?? throw new InvalidOperationException("BackendUrl not configured");
        }
        else
        {
            string[] strings = host.Split('.');
            tenantIdentifier = strings.Length > 2 ? strings[0] : "api";
            string remoteHostUrl = configuration["RemoteHostUrl"] ?? "api.getkrafter.dev";
            backendUrl = $"https://{tenantIdentifier}.{remoteHostUrl}";
            clientBaseAddress = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        }

        string prefix = tenantIdentifier + ".";
        string rootDomain = host.StartsWith(prefix) ? host.Substring(prefix.Length) : host;
        return (tenantIdentifier, backendUrl, rootDomain, clientBaseAddress);
    }
}
