using Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

public interface IRolesApi
{
    [Get("/roles")]
    public Task<Response<PaginationResponse<RoleDto>>> GetRolesAsync(
        [Query] GetRequestInput request,
        CancellationToken cancellationToken = default);

    [Post("/roles")]
    public Task<Response> CreateRoleAsync([Body] CreateOrUpdateRoleRequest request,
        CancellationToken cancellationToken = default);

    [Put("/roles/{id}")]
    public Task<Response> UpdateRoleAsync(string id, [Body] CreateOrUpdateRoleRequest request,
        CancellationToken cancellationToken = default);

    [Delete("/roles/{id}")]
    public Task<Response> DeleteRoleAsync(string id,
        CancellationToken cancellationToken = default);

    [Get("/roles/{roleId}/permissions")]
    public Task<Response<RoleDto>> GetRolePermissionsAsync(string roleId,
        CancellationToken cancellationToken = default);

    [Put("/roles/permissions")]
    public Task<Response> UpdateRolePermissionsAsync([Body] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default);
}
