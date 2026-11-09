using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Users;

public sealed class GetUserRoles
{
    internal sealed class Handler(ApplicationDbContext db) : IScopedHandler
    {
        public async Task<Response<List<UserRoleDto>>> GetRolesAsync(
            string userId, CancellationToken cancellationToken)
        {
            if (!await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken))
            {
                return new Response<List<UserRoleDto>> { IsError = true, Message = "User Not Found", StatusCode = 404 };
            }

            // A role rename must not remove its membership from the editor's baseline.
            // Resolve IDs and display fields in one tenant-filtered snapshot.
            List<UserRoleDto> userRoles = await (
                from membership in db.UserRoles.AsNoTracking()
                join role in db.Roles on membership.RoleId equals role.Id
                where membership.UserId == userId
                select new UserRoleDto
                {
                    RoleId = role.Id,
                    RoleName = role.Name,
                    Description = role.Description,
                    Enabled = true
                }).ToListAsync(cancellationToken);

            return new Response<List<UserRoleDto>> { Data = userRoles };
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(ApiRoutes.Users)
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
                .MustHavePermission(PermissionAction.View, PermissionResource.UserRoles);
        }
    }
}



