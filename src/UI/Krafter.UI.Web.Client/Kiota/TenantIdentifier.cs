using Krafter.UI.Web.Client.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Krafter.UI.Web.Client.Kiota;

public class TenantIdentifier(IServiceProvider serviceProvider,IConfiguration configuration)
{

    public(string tenantIdentifier, string hostUrl, string host) Get()
    {
        var formFactor = serviceProvider.GetRequiredService<IFormFactor>();
        string navigationManagerBaseUri;
        string tenantIdentifier;
        var formFactorType = formFactor.GetFormFactor();

        if (formFactorType == "Web")
        {
            var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
            var httpRequest = httpContextAccessor.HttpContext?.Request;
            if (httpRequest == null)
                throw new Exception("Request is null");

            navigationManagerBaseUri = $"{httpRequest.Scheme}://{httpRequest.Host}";
        }
        else if (formFactorType == "WebAssembly")
        {
            var navigationManager = serviceProvider.GetRequiredService<NavigationManager>();
            navigationManagerBaseUri = navigationManager.BaseUri;
        }
        else
        {
            navigationManagerBaseUri = "https://krafter.getkrafter.dev";
        }

        var uri = new Uri(navigationManagerBaseUri);
        var host = uri.Host;
        string hostUrl;
        var isRunningLocally = host == "localhost" || host == "127.0.0.1";

        if (isRunningLocally)
        {
            tenantIdentifier = "krafter"; // adjust if you want different local logic
            hostUrl = configuration["RemoteHostUrl"];
        }
        else
        {
            var strings = host.Split('.'); tenantIdentifier = strings.Length > 2 ? strings[0] : "api";
            tenantIdentifier = "krafter"; // adjust if you want different local logic

            hostUrl = $"https://{tenantIdentifier}.{configuration["RemoteHostUrl"]}";
        }
        return (tenantIdentifier, hostUrl,host);
    }
}