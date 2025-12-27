using Backend.Api;
using Backend.Api.Authorization;
using Backend.Common;
using Backend.Features.Roles._Shared;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using Krafter.Shared.Common;
using Krafter.Shared.Common.Auth.Permissions;
using Krafter.Shared.Common.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Users;

public sealed class GetUserRoles
{
    internal sealed class Handler(
        UserManager<KrafterUser> userManager,
        RoleManager<KrafterRole> roleManager) : IScopedHandler
    {
        public async Task<Response<List<Krafter.Shared.Features.Users.GetUserRoles.UserRoleDto>>> GetRolesAsync(
            string userId, CancellationToken cancellationToken)
        {
            KrafterUser? user = await userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return new Response<List<Krafter.Shared.Features.Users.GetUserRoles.UserRoleDto>>
                {
                    IsError = true, Message = "User Not Found", StatusCode = 404
                };
            }

            IList<string> userRoleNames = await userManager.GetRolesAsync(user);
            List<KrafterRole>? roles = await roleManager.Roles
                .Where(c => userRoleNames.Contains(c.Name))
                .ToListAsync(cancellationToken);

            if (roles is null || !roles.Any())
            {
                return new Response<List<Krafter.Shared.Features.Users.GetUserRoles.UserRoleDto>>
                {
                    Data = new List<Krafter.Shared.Features.Users.GetUserRoles.UserRoleDto>()
                };
            }

            var userRoles = new List<Krafter.Shared.Features.Users.GetUserRoles.UserRoleDto>();
            foreach (KrafterRole role in roles)
            {
                userRoles.Add(new Krafter.Shared.Features.Users.GetUserRoles.UserRoleDto
                {
                    RoleId = role.Id,
                    RoleName = role.Name,
                    Description = role.Description,
                    Enabled = await userManager.IsInRoleAsync(user, role.Name!)
                });
            }

            return new Response<List<Krafter.Shared.Features.Users.GetUserRoles.UserRoleDto>> { Data = userRoles };
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Users)
                .AddFluentValidationFilter();

            userGroup.MapGet("/get-roles/{userId}", async (
                    [FromRoute] string userId,
                    [FromServices] Handler handler,
                    CancellationToken cancellationToken) =>
                {
                    Response<List<Krafter.Shared.Features.Users.GetUserRoles.UserRoleDto>> res =
                        await handler.GetRolesAsync(userId, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<List<Krafter.Shared.Features.Users.GetUserRoles.UserRoleDto>>>()
                .MustHavePermission(KrafterAction.View, KrafterResource.UserRoles);
        }
    }
}
