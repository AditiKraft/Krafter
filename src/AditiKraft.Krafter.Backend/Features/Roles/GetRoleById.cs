using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Roles;

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

            roleGroup.MapGet($"/{RouteSegment.ById}", async (
                    [FromRoute] string id,
                    [FromServices] Handler handler) =>
                {
                    Response<RoleDto> res = await handler.GetByIdAsync(id);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<RoleDto>>()
                .MustHavePermission(KrafterAction.View, KrafterResource.Roles);
        }
    }
}



