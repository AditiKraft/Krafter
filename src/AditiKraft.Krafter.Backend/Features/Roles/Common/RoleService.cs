using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Roles.Common;

public class RoleService(
    KrafterContext db)
    : IRoleService, IScopedService
{
    public async Task<Response<RoleDto>> GetByIdAsync(string id)
    {
        KrafterRole? res = await db.Roles.SingleOrDefaultAsync(x => x.Id == id);
        if (res is not null)
        {
            return Response<RoleDto>.Success(res.Adapt<RoleDto>());
        }

        return Response<RoleDto>.NotFound("Role Not Found");
    }
}


