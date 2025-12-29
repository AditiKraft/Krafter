using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Tenants;
using Refit;

namespace Krafter.UI.Web.Client.Infrastructure.Refit;

public interface ITenantsApi
{
    [Get("/tenants/get")]
    public Task<Response<PaginationResponse<TenantDto>>> GetTenantsAsync(
        [Query] string? id = null,
        [Query] string? filter = null,
        [Query] string? orderBy = null,
        [Query] int skipCount = 0,
        [Query] int maxResultCount = 10,
        [Query] bool history = false,
        [Query] bool isDeleted = false,
        CancellationToken cancellationToken = default);

    [Post("/tenants/create-or-update")]
    public Task<Response> CreateOrUpdateTenantAsync([Body] CreateOrUpdateTenantRequest request,
        CancellationToken cancellationToken = default);

    [Post("/tenants/delete")]
    public Task<Response> DeleteTenantAsync([Body] DeleteRequestInput request,
        CancellationToken cancellationToken = default);

    [Post("/tenants/seed-data")]
    public Task<Response> SeedDataAsync([Body] SeedDataRequest request, CancellationToken cancellationToken = default);
}
