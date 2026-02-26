using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;

namespace AditiKraft.Krafter.Backend.Features.Roles._Shared;

public interface IRoleService
{
    public Task<Response<RoleDto>> GetByIdAsync(string id);
}
