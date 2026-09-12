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

public sealed class CreateRole
{
    internal sealed class Handler(
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext db,
        RolePermissionService permissionService) : IScopedHandler
    {
        public async Task<Response> CreateAsync(CreateOrUpdateRoleRequest request, CancellationToken cancellationToken)
        {
            request.Id = null;
            if (request.Permissions is not null && request.Name == RoleConstants.Admin)
            {
                return Response.BadRequest("Not allowed to modify Permissions for this Role.");
            }

            var role = new ApplicationRole(request.Name, request.Description) { Id = Guid.NewGuid().ToString() };

            IdentityResult result = await roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                return Response.BadRequest($"Register role failed {result.Errors.ToString()}");
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

            roleGroup.MapPost("/", async (
                    [FromBody] CreateOrUpdateRoleRequest request,
                    [FromServices] Handler handler,
                    CancellationToken cancellationToken) =>
                {
                    Response res = await handler.CreateAsync(request, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .MustHavePermission(PermissionAction.Create, PermissionResource.Roles);
        }
    }
}
