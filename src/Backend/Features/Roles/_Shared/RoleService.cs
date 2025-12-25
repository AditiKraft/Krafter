using System.Linq.Dynamic.Core;
using Backend.Application.Common;
using Backend.Common.Auth;
using Backend.Common.Extensions;
using Backend.Common.Interfaces;
using Backend.Common.Models;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using LinqKit;
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
            return new Response<RoleDto> { Data = res.Adapt<RoleDto>() };
        }

        throw new NotFoundException("Role Not Found");
    }
}
