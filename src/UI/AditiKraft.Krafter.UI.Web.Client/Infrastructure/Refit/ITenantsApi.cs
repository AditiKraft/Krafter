using Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

public interface ITenantsApi
{
    [Get("/api/tenants")]
    public Task<Response<PaginationResponse<TenantDto>>> GetTenantsAsync(
        [Query] GetRequestInput request,
        CancellationToken cancellationToken = default);

    [Post("/api/tenants")]
    public Task<Response> CreateTenantAsync([Body] CreateOrUpdateTenantRequest request,
        CancellationToken cancellationToken = default);

    [Put("/api/tenants/{id}")]
    public Task<Response> UpdateTenantAsync(string id, [Body] CreateOrUpdateTenantRequest request,
        CancellationToken cancellationToken = default);

    [Delete("/api/tenants/{id}")]
    public Task<Response> DeleteTenantAsync(string id,
        CancellationToken cancellationToken = default);

    [Post("/api/tenants/seed-data")]
    public Task<Response> SeedDataAsync([Body] SeedDataRequest request, CancellationToken cancellationToken = default);
}
