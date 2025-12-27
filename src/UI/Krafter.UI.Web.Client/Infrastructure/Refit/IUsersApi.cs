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
    public Task<Response<PaginationResponse<UserDto>>> GetUsersAsync(
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
    public Task<Response<PaginationResponse<UserInfo>>> GetUsersByRoleAsync(
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
    public Task<Response> CreateOrUpdateUserAsync([Body] CreateUserRequest request,
        CancellationToken cancellationToken = default);

    [Post("/users/delete")]
    public Task<Response> DeleteUserAsync([Body] DeleteRequestInput request,
        CancellationToken cancellationToken = default);

    [Get("/users/permissions")]
    public Task<Response<List<string>>> GetPermissionsAsync(CancellationToken cancellationToken = default);

    [Get("/users/roles")]
    public Task<Response<List<UserRoleDto>>> GetUserRolesAsync([Query] string userId,
        CancellationToken cancellationToken = default);

    [Post("/users/change-password")]
    public Task<Response> ChangePasswordAsync([Body] ChangePasswordRequest request,
        CancellationToken cancellationToken = default);

    [Post("/users/forgot-password")]
    public Task<Response> ForgotPasswordAsync([Body] ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);

    [Post("/users/reset-password")]
    public Task<Response> ResetPasswordAsync([Body] ResetPasswordRequest request,
        CancellationToken cancellationToken = default);
}
