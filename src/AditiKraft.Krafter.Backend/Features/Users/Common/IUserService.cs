using AditiKraft.Krafter.Contracts.Common.Models;

namespace AditiKraft.Krafter.Backend.Features.Users.Common;

public interface IUserService
{
    public Task<Response<List<string>>> GetPermissionsAsync(string userId, CancellationToken cancellationToken);

    public Task<Response<bool>> HasPermissionAsync(string userId, string permission,
        CancellationToken cancellationToken = default);

}


