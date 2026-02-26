using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Users;

namespace AditiKraft.Krafter.Backend.Features.Users._Shared;

public interface IUserService
{
    public Task<Response<List<string>>> GetPermissionsAsync(string userId, CancellationToken cancellationToken);

    public Task<Response<bool>> HasPermissionAsync(string userId, string permission,
        CancellationToken cancellationToken = default);

    public Task<Response> CreateOrUpdateAsync(CreateUserRequest request);
}
