using AditiKraft.Krafter.Backend.Api;
using AditiKraft.Krafter.Backend.Features.Roles._Shared;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Api.Authorization;
using AditiKraft.Krafter.Backend.Common;
using AditiKraft.Krafter.Shared.Common;
using AditiKraft.Krafter.Shared.Common.Auth;
using AditiKraft.Krafter.Shared.Common.Auth.Permissions;
using AditiKraft.Krafter.Shared.Common.Models;
using AditiKraft.Krafter.Shared.Contracts.Roles;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Roles;

public sealed class GetRoleByIdWithPermissions
{
    internal sealed class Handler(
        RoleManager<KrafterRole> roleManager,
        KrafterContext db) : IScopedHandler
    {
        public async Task<Response<RoleDto>> GetByIdWithPermissionsAsync(
            string roleId,
            CancellationToken cancellationToken)
        {
            KrafterRole? role = await roleManager.Roles.SingleOrDefaultAsync(x => x.Id == roleId, cancellationToken);

            if (role is null)
            {
                return new Response<RoleDto> { IsError = true, StatusCode = 404, Message = "Role Not Found" };
            }

            RoleDto roleDto = role.Adapt<RoleDto>();

            roleDto.Permissions = await db.RoleClaims
                .Where(c => c.RoleId == roleId &&
                            c.ClaimType == KrafterClaims.Permission &&
                            c.IsDeleted == false)
                .Select(c => c.ClaimValue!)
                .ToListAsync(cancellationToken);

            return new Response<RoleDto> { Data = roleDto };
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder roleGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Roles)
                .AddFluentValidationFilter();

            roleGroup.MapGet($"/{RouteSegment.RolePermissions}", async (
                    [FromRoute] string roleId,
                    [FromServices] Handler handler,
                    CancellationToken cancellationToken) =>
                {
                    Response<RoleDto> res = await handler.GetByIdWithPermissionsAsync(roleId, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<RoleDto>>()
                .MustHavePermission(KrafterAction.View, KrafterResource.Roles);
        }
    }
}
