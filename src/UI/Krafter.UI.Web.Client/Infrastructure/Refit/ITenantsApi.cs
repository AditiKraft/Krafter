using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Tenants;
using Refit;

namespace Krafter.UI.Web.Client.Infrastructure.Refit;

public interface ITenantsApi
{
    [Get("/tenants")]
    public Task<Response<PaginationResponse<TenantDto>>> GetTenantsAsync(
        [Query] GetRequestInput request,
        CancellationToken cancellationToken = default);

    [Post("/tenants")]
    public Task<Response> CreateOrUpdateTenantAsync([Body] CreateOrUpdateTenantRequest request,
        CancellationToken cancellationToken = default);

    [Delete("/tenants/{id}")]
    public Task<Response> DeleteTenantAsync(string id,
        CancellationToken cancellationToken = default);

    [Post("/tenants/seed-data")]
    public Task<Response> SeedDataAsync([Body] SeedDataRequest request, CancellationToken cancellationToken = default);
}
