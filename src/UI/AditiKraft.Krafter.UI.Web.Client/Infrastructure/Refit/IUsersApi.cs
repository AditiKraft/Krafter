using Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;

public interface IUsersApi
{
    [Get("/users")]
    public Task<Response<PaginationResponse<UserDto>>> GetUsersAsync(
        [Query] GetRequestInput request,
        CancellationToken cancellationToken = default);

    [Get("/users/by-role/{roleId}")]
    public Task<Response<PaginationResponse<UserInfo>>> GetUsersByRoleAsync(
        string roleId,
        [Query] GetRequestInput request,
        CancellationToken cancellationToken = default);

    [Post("/users")]
    public Task<Response> CreateUserAsync([Body] CreateUserRequest request,
        CancellationToken cancellationToken = default);

    [Put("/users/{id}")]
    public Task<Response> UpdateUserAsync(string id, [Body] CreateUserRequest request,
        CancellationToken cancellationToken = default);

    [Delete("/users/{id}")]
    public Task<Response> DeleteUserAsync(string id,
        CancellationToken cancellationToken = default);

    [Get("/users/permissions")]
    public Task<Response<List<string>>> GetPermissionsAsync(CancellationToken cancellationToken = default);

    [Get("/users/{userId}/roles")]
    public Task<Response<List<UserRoleDto>>> GetUserRolesAsync(string userId,
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
