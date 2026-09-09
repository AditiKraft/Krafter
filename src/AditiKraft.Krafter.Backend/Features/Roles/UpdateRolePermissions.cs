using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AditiKraft.Krafter.Backend.Features.Roles;

public sealed class UpdateRolePermissions
{
    internal sealed class Handler(
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext db,
        RolePermissionService permissionService) : IScopedHandler
    {
        public async Task<Response> UpdatePermissionsAsync(
            UpdateRolePermissionsRequest request,
            CancellationToken cancellationToken)
        {
            ApplicationRole? role = await roleManager.FindByIdAsync(request.RoleId);

            if (role is null)
            {
                return new Response { IsError = true, StatusCode = 404, Message = "Role Not Found" };
            }

            if (role.Name == RoleConstants.Admin)
            {
                return new Response
                {
                    IsError = true, StatusCode = 403, Message = "Not allowed to modify Permissions for this Role."
                };
            }

            await permissionService.SynchronizeAsync(role.Id, request.Permissions, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            return new Response { Message = "Role permissions updated successfully" };
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder roleGroup = endpointRouteBuilder.MapGroup(ApiRoutes.Roles)
                .AddFluentValidationFilter();

            roleGroup.MapPut("/permissions", async (
                    [FromBody] UpdateRolePermissionsRequest request,
                    [FromServices] Handler handler,
                    CancellationToken cancellationToken) =>
                {
                    Response res = await handler.UpdatePermissionsAsync(request, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .MustHavePermission(PermissionAction.Update, PermissionResource.Roles);
        }
    }
}



