using Backend.Api;
using Backend.Api.Authorization;
using Backend.Common;
using Backend.Common.Auth.Permissions;
using Backend.Common.Models;
using Backend.Features.Roles._Shared;
using Backend.Infrastructure.Persistence;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Roles;

public sealed class GetRoleById
{
    internal sealed class Handler(RoleManager<KrafterRole> roleManager) : IScopedHandler
    {
        public async Task<Response<RoleDto>> GetByIdAsync(string id)
        {
            KrafterRole? role = await roleManager.Roles.SingleOrDefaultAsync(x => x.Id == id);

            if (role is null)
            {
                return new Response<RoleDto> { IsError = true, StatusCode = 404, Message = "Role Not Found" };
            }

            return new Response<RoleDto> { Data = role.Adapt<RoleDto>() };
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder roleGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Roles)
                .AddFluentValidationFilter();

            roleGroup.MapGet("/get-by-id/{roleId}", async (
                    [FromRoute] string roleId,
                    [FromServices] Handler handler) =>
                {
                    Response<RoleDto> res = await handler.GetByIdAsync(roleId);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<RoleDto>>()
                .MustHavePermission(KrafterAction.View, KrafterResource.Roles);
        }
    }
}
