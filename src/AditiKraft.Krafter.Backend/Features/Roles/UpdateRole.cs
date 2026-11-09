using AditiKraft.Krafter.Backend.Common.Tenants;
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
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Roles;

public sealed class UpdateRole
{
    internal sealed class Handler(
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext db,
        RolePermissionService permissionService,
        ITenantGetterService tenantGetterService) : IScopedHandler
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

            if (request.Permissions is not null)
            {
                Response validation = await permissionService.ValidatePermissionsAsync(request.Permissions, cancellationToken);
                if (validation.IsError)
                {
                    return validation;
                }
            }

            if (RoleConstants.IsDefault(role.Name!) &&
                (request.Name != role.Name || request.Description != role.Description))
                return Response.Forbidden($"Not allowed to modify {role.Name} Role.");

            string normalizedName = roleManager.NormalizeKey(request.Name);
            if (await db.Roles.IgnoreQueryFilters().AnyAsync(candidate => candidate.Id != role.Id &&
                    candidate.TenantId == tenantGetterService.Tenant.Id && candidate.NormalizedName == normalizedName,
                    cancellationToken))
                return Response.Conflict(IdentityConflict.RoleMessage);

            await using var transaction = await DomainTransaction.BeginAsync(db, cancellationToken);
            if (request.Name != role.Name)
            {
                role.Name = request.Name;
            }

            if (request.Description != role.Description)
            {
                role.Description = request.Description;
            }

            try
            {
                IdentityResult result = await roleManager.UpdateAsync(role);
                if (!result.Succeeded)
                {
                    db.Entry(role).State = EntityState.Detached;
                    return IdentityConflict.IsDuplicate(result)
                        ? Response.Conflict(IdentityConflict.RoleMessage)
                        : Response.BadRequest("Update role failed: " + string.Join(" ", result.Errors.Select(error => error.Description)));
                }

                if (request.Permissions is not null)
                {
                    Response permissions = await permissionService.SynchronizeAsync(role.Id, request.Permissions, cancellationToken);
                    if (permissions.IsError)
                        return permissions;
                }

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Response.Success();
            }
            catch (DbUpdateException exception) when (IdentityConflict.IsRole(exception))
            {
                return Response.Conflict(IdentityConflict.RoleMessage);
            }
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
