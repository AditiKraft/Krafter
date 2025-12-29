using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Roles;
using Refit;

namespace Krafter.UI.Web.Client.Infrastructure.Refit;

public interface IRolesApi
{
    [Get("/roles/get")]
    public Task<Response<PaginationResponse<RoleDto>>> GetRolesAsync(
        [Query] string? id = null,
        [Query] bool history = false,
        [Query] bool isDeleted = false,
        [Query] string? query = null,
        [Query] string? filter = null,
        [Query] string? orderBy = null,
        [Query] int skipCount = 0,
        [Query] int maxResultCount = 10,
        CancellationToken cancellationToken = default);

    [Post("/roles/create-or-update")]
    public Task<Response> CreateOrUpdateRoleAsync([Body] CreateOrUpdateRoleRequest request,
        CancellationToken cancellationToken = default);

    [Post("/roles/delete")]
    public Task<Response> DeleteRoleAsync([Body] DeleteRequestInput request,
        CancellationToken cancellationToken = default);

    [Get("/roles/get-by-id-with-permissions/{roleId}")]
    public Task<Response<RoleDto>> GetRolePermissionsAsync(string roleId,
        CancellationToken cancellationToken = default);

    [Put("/roles/update-permissions")]
    public Task<Response> UpdateRolePermissionsAsync([Body] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default);
}
