using Krafter.Shared.Common.Models;
using Refit;

namespace Krafter.UI.Web.Client.Infrastructure.Refit;

/// <summary>
/// Refit interface for application info endpoints.
/// </summary>
public interface IAppInfoApi
{
    [Get("/app-info")]
    Task<Response<string>> GetAppInfoAsync(CancellationToken cancellationToken = default);
}
