using AditiKraft.Krafter.Backend.Api;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Api.Authorization;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Users;

public sealed class GetUserRoles
{
    internal sealed class Handler(
        UserManager<KrafterUser> userManager,
        RoleManager<KrafterRole> roleManager) : IScopedHandler
    {
        public async Task<Response<List<UserRoleDto>>> GetRolesAsync(
            string userId, CancellationToken cancellationToken)
        {
            KrafterUser? user = await userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return new Response<List<UserRoleDto>> { IsError = true, Message = "User Not Found", StatusCode = 404 };
            }

            IList<string> userRoleNames = await userManager.GetRolesAsync(user);
            List<KrafterRole>? roles = await roleManager.Roles
                .Where(c => c.Name != null && userRoleNames.Contains(c.Name))
                .ToListAsync(cancellationToken);

            if (roles is null || !roles.Any())
            {
                return new Response<List<UserRoleDto>> { Data = new List<UserRoleDto>() };
            }

            var userRoles = new List<UserRoleDto>();
            foreach (KrafterRole role in roles)
            {
                userRoles.Add(new UserRoleDto
                {
                    RoleId = role.Id,
                    RoleName = role.Name,
                    Description = role.Description,
                    Enabled = await userManager.IsInRoleAsync(user, role.Name!)
                });
            }

            return new Response<List<UserRoleDto>> { Data = userRoles };
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Users)
                .AddFluentValidationFilter();

            userGroup.MapGet($"/{RouteSegment.UserRoles}", async (
                    [FromRoute] string userId,
                    [FromServices] Handler handler,
                    CancellationToken cancellationToken) =>
                {
                    Response<List<UserRoleDto>> res =
                        await handler.GetRolesAsync(userId, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<List<UserRoleDto>>>()
                .MustHavePermission(KrafterAction.View, KrafterResource.UserRoles);
        }
    }
}


