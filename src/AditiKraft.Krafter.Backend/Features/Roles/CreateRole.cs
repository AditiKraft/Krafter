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

public sealed class CreateRole
{
    internal sealed class Handler(
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext db,
        RolePermissionService permissionService,
        ITenantGetterService tenantGetterService) : IScopedHandler
    {
        public async Task<Response> CreateAsync(CreateOrUpdateRoleRequest request, CancellationToken cancellationToken)
        {
            request.Id = null;
            if (request.Permissions is not null && request.Name == RoleConstants.Admin)
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

            string normalizedName = roleManager.NormalizeKey(request.Name);
            if (await db.Roles.IgnoreQueryFilters().AnyAsync(role =>
                    role.TenantId == tenantGetterService.Tenant.Id && role.NormalizedName == normalizedName,
                    cancellationToken))
                return Response.Conflict(IdentityConflict.RoleMessage);

            var role = new ApplicationRole(request.Name, request.Description) { Id = Guid.NewGuid().ToString() };
            await using var transaction = await DomainTransaction.BeginAsync(db, cancellationToken);
            try
            {
                IdentityResult result = await roleManager.CreateAsync(role);
                if (!result.Succeeded)
                    return IdentityConflict.IsDuplicate(result)
                        ? Response.Conflict(IdentityConflict.RoleMessage)
                        : Response.BadRequest("Register role failed: " + string.Join(" ", result.Errors.Select(error => error.Description)));

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
