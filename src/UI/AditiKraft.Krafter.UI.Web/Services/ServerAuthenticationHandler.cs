using System.Net.Http.Headers;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Storage;

namespace AditiKraft.Krafter.UI.Web.Services;

public class ServerAuthenticationHandler(
    IKrafterLocalStorageService localStorage,
    ILogger<ServerAuthenticationHandler> logger)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        bool isToServer = request.RequestUri?.AbsoluteUri.StartsWith(TenantInfo.HostUrl ?? "") ?? false;

        if (isToServer)
        {
            // For server-side, token should always be fresh due to middleware
            string? jwt = await localStorage.GetCachedAuthTokenAsync();

            if (!string.IsNullOrEmpty(jwt))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
                logger.LogDebug("Server handler - added fresh token to request");
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
