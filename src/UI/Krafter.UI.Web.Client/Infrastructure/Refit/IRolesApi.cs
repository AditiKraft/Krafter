using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Roles;
using Refit;

namespace Krafter.UI.Web.Client.Infrastructure.Refit;

/// <summary>
/// Refit interface for role management endpoints.
/// </summary>
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

    [Get("/roles/permissions")]
    public Task<Response<List<string>>> GetRolePermissionsAsync([Query] string roleId,
        CancellationToken cancellationToken = default);

    [Post("/roles/update-permissions")]
    public Task<Response> UpdateRolePermissionsAsync([Body] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default);
}
