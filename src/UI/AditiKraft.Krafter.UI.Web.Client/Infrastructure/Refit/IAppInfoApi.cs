using Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

public interface IAppInfoApi
{
    [Get("/app-info")]
    public Task<Response<string>> GetAppInfoAsync(CancellationToken cancellationToken = default);
}
