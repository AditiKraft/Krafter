using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AditiKraft.Krafter.Backend.Features.Roles;

public sealed class UpdateRole
{
    internal sealed class Handler(
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext db,
        RolePermissionService permissionService) : IScopedHandler
    {
        public async Task<Response> UpdateAsync(string id, CreateOrUpdateRoleRequest request,
            CancellationToken cancellationToken)
        {
            request.Id = id;
            ApplicationRole? role = await roleManager.FindByIdAsync(id);
            if (role == null)
            {
                return Response.NotFound("Role Not Found");
            }

            if (request.Permissions is not null && role.Name == RoleConstants.Admin)
            {
                return Response.BadRequest("Not allowed to modify Permissions for this Role.");
            }

            if (request.Name != role.Name)
            {
                if (RoleConstants.IsDefault(role.Name!))
                {
                    return Response.Forbidden($"Not allowed to modify {role.Name} Role.");
                }

                role.Name = request.Name;
                role.NormalizedName = request.Name?.ToUpperInvariant();
            }

            if (request.Description != role.Description)
            {
                if (RoleConstants.IsDefault(role.Name!))
                {
                    return Response.Forbidden($"Not allowed to modify {role.Name} Role.");
                }

                role.Description = request.Description;
            }

            IdentityResult result = await roleManager.UpdateAsync(role);
            if (!result.Succeeded)
            {
                return Response.BadRequest($"Update role failed {result.Errors.ToString()}");
            }

            if (request.Permissions is not null)
            {
                await permissionService.SynchronizeAsync(role.Id, request.Permissions, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            return new Response();
        }

    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder roleGroup = endpointRouteBuilder.MapGroup(ApiRoutes.Roles)
                .AddFluentValidationFilter();

            roleGroup.MapPut($"/{RouteSegment.ById}", async (
                    [FromRoute] string id,
                    [FromBody] CreateOrUpdateRoleRequest request,
                    [FromServices] Handler handler,
                    CancellationToken cancellationToken) =>
                {
                    Response res = await handler.UpdateAsync(id, request, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .MustHavePermission(PermissionAction.Update, PermissionResource.Roles);
        }
    }
}
