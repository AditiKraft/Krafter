using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Roles;

namespace Backend.Features.Roles._Shared;

public interface IRoleService
{
    public Task<Response<RoleDto>> GetByIdAsync(string id);
}
