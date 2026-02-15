using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Features.Users._Shared;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Shared.Common.Models;
using AditiKraft.Krafter.Shared.Contracts.Roles;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Roles._Shared;

public class RoleService(
    RoleManager<KrafterRole> roleManager,
    UserManager<KrafterUser> userManager,
    KrafterContext db,
    ITenantGetterService tenantGetterService)
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
