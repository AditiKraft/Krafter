using AditiKraft.Krafter.Shared.Common.Models;
using AditiKraft.Krafter.Shared.Contracts.Roles;

namespace AditiKraft.Krafter.Backend.Features.Roles._Shared;

public interface IRoleService
{
    public Task<Response<RoleDto>> GetByIdAsync(string id);
}
