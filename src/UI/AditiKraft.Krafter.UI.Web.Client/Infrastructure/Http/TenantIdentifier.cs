using System.Net;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Enums;
using Microsoft.AspNetCore.Http;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;

public class TenantIdentifier(IServiceProvider serviceProvider, IConfiguration configuration)
{
    public (string tenantIdentifier, string backendUrl, string rootDomain, string clientBaseAddress, bool isServerSide)
        Get()
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
        bool isRunningLocally = IsLocalHost(host);
        string clientBaseAddress;

        string remoteHostUrl = configuration["RemoteHostUrl"] ??
                               throw new InvalidOperationException("RemoteHostUrl not configured");
        bool isRemoteHostFullUrl = Uri.TryCreate(remoteHostUrl, UriKind.Absolute, out Uri? remoteHostUri);

        if (TenantSettings.TenancyMode == TenancyMode.Single)
        {
            tenantIdentifier = KrafterTenantConstants.Identifier;
            clientBaseAddress = navigationManagerBaseUri;
            backendUrl = ToAbsoluteUrl(remoteHostUrl, remoteHostUri);
        }
        else if (isRunningLocally)
        {
            tenantIdentifier = KrafterTenantConstants.Identifier;
            clientBaseAddress = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
            backendUrl = ToAbsoluteUrl(remoteHostUrl, remoteHostUri);
        }
        else
        {
            string[] strings = host.Split('.');
            tenantIdentifier = strings.Length > 2 ? strings[0] : "api";
            clientBaseAddress = $"{uri.Scheme}://{uri.Host}:{uri.Port}";

            if (isRemoteHostFullUrl)
            {
                if (isServerSide || remoteHostUri is null || IsLocalHost(remoteHostUri.Host) || !CanAddTenantSubdomain(remoteHostUri.Host, tenantIdentifier))
                {
                    backendUrl = ToAbsoluteUrl(remoteHostUrl, remoteHostUri);
                }
                else
                {
                    UriBuilder builder = new(remoteHostUri)
                    {
                        Host = $"{tenantIdentifier}.{remoteHostUri.Host}"
                    };
                    backendUrl = builder.Uri.AbsoluteUri.TrimEnd('/');
                }
            }
            else
            {
                backendUrl = $"https://{tenantIdentifier}.{remoteHostUrl}";
            }
        }

        string rootDomain;
        if (TenantSettings.TenancyMode == TenancyMode.Single)
        {
            rootDomain = host;
        }
        else
        {
            string prefix = tenantIdentifier + ".";
            rootDomain = host.StartsWith(prefix) ? host.Substring(prefix.Length) : host;
        }

        return (tenantIdentifier, backendUrl, rootDomain, clientBaseAddress, isServerSide);
    }

    private static bool IsLocalHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address);
    }

    private static bool CanAddTenantSubdomain(string host, string tenantIdentifier)
    {
        UriHostNameType hostType = Uri.CheckHostName(host);
        if (hostType != UriHostNameType.Dns)
        {
            return false;
        }

        return !host.StartsWith(tenantIdentifier + ".", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToAbsoluteUrl(string remoteHostUrl, Uri? remoteHostUri)
    {
        if (remoteHostUri is not null)
        {
            return remoteHostUri.AbsoluteUri.TrimEnd('/');
        }

        return $"https://{remoteHostUrl.TrimEnd('/')}";
    }
}
