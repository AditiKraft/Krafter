using Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Features.AppInfo;

public interface IAppInfoApi
{
    [Get("/api/app-info")]
    public Task<Response<string>> GetAppInfoAsync(CancellationToken cancellationToken = default);
}
