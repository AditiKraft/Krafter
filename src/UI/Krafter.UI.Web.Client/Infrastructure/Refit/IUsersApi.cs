using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Users;
using Refit;

namespace Krafter.UI.Web.Client.Infrastructure.Refit;

/// <summary>
/// Refit interface for user management endpoints.
/// </summary>
public interface IUsersApi
{
    [Get("/users/get")]
    Task<Response<PaginationResponse<UserDto>>> GetUsersAsync(
        [Query] string? id = null,
        [Query] bool history = false,
        [Query] bool isDeleted = false,
        [Query] string? query = null,
        [Query] string? filter = null,
        [Query] string? orderBy = null,
        [Query] int skipCount = 0,
        [Query] int maxResultCount = 10,
        CancellationToken cancellationToken = default);

    [Get("/users/by-role/{roleId}")]
    Task<Response<PaginationResponse<UserInfo>>> GetUsersByRoleAsync(
        string roleId,
        [Query] string? id = null,
        [Query] bool history = false,
        [Query] bool isDeleted = false,
        [Query] string? query = null,
        [Query] string? filter = null,
        [Query] string? orderBy = null,
        [Query] int skipCount = 0,
        [Query] int maxResultCount = 10,
        CancellationToken cancellationToken = default);

    [Post("/users/create-or-update")]
    Task<Response> CreateOrUpdateUserAsync([Body] CreateUserRequest request, CancellationToken cancellationToken = default);

    [Post("/users/delete")]
    Task<Response> DeleteUserAsync([Body] DeleteRequestInput request, CancellationToken cancellationToken = default);

    [Get("/users/permissions")]
    Task<Response<List<string>>> GetPermissionsAsync(CancellationToken cancellationToken = default);

    [Get("/users/roles")]
    Task<Response<List<UserRoleDto>>> GetUserRolesAsync([Query] string userId, CancellationToken cancellationToken = default);

    [Post("/users/change-password")]
    Task<Response> ChangePasswordAsync([Body] ChangePasswordRequest request, CancellationToken cancellationToken = default);

    [Post("/users/forgot-password")]
    Task<Response> ForgotPasswordAsync([Body] ForgotPasswordRequest request, CancellationToken cancellationToken = default);

    [Post("/users/reset-password")]
    Task<Response> ResetPasswordAsync([Body] ResetPasswordRequest request, CancellationToken cancellationToken = default);
}
