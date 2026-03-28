using Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

public interface IRolesApi
{
    [Get("/api/roles")]
    public Task<Response<PaginationResponse<RoleDto>>> GetRolesAsync(
        [Query] GetRequestInput request,
        CancellationToken cancellationToken = default);

    [Post("/api/roles")]
    public Task<Response> CreateRoleAsync([Body] CreateOrUpdateRoleRequest request,
        CancellationToken cancellationToken = default);

    [Put("/api/roles/{id}")]
    public Task<Response> UpdateRoleAsync(string id, [Body] CreateOrUpdateRoleRequest request,
        CancellationToken cancellationToken = default);

    [Delete("/api/roles/{id}")]
    public Task<Response> DeleteRoleAsync(string id,
        CancellationToken cancellationToken = default);

    [Get("/api/roles/{roleId}/permissions")]
    public Task<Response<RoleDto>> GetRolePermissionsAsync(string roleId,
        CancellationToken cancellationToken = default);

    [Put("/api/roles/permissions")]
    public Task<Response> UpdateRolePermissionsAsync([Body] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default);
}
