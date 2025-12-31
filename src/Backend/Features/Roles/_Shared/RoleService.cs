using Backend.Common.Interfaces;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Roles;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Roles._Shared;

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
