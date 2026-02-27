using AditiKraft.Krafter.Backend.Api;
using AditiKraft.Krafter.Backend.Api.Authorization;
using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
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
        RoleManager<KrafterRole> roleManager,
        KrafterContext db,
        ITenantGetterService tenantGetterService) : IScopedHandler
    {
        public async Task<Response> UpdateAsync(string id, CreateOrUpdateRoleRequest request,
            CancellationToken cancellationToken)
        {
            request.Id = id;
            KrafterRole? role = await roleManager.FindByIdAsync(id);
            if (role == null)
            {
                return Response.NotFound("Role Not Found");
            }

            if (request.Name != role.Name)
            {
                if (KrafterRoleConstant.IsDefault(role.Name!))
                {
                    return Response.Forbidden($"Not allowed to modify {role.Name} Role.");
                }

                role.Name = request.Name;
                role.NormalizedName = request.Name?.ToUpperInvariant();
            }

            if (request.Description != role.Description)
            {
                if (KrafterRoleConstant.IsDefault(role.Name!))
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

            if (request.Permissions is { Count: > 0 })
            {
                if (role.Name == KrafterRoleConstant.Admin)
                {
                    return Response.BadRequest("Not allowed to modify Permissions for this Role.");
                }

                string tenantId = tenantGetterService.Tenant.Id ?? string.Empty;
                await SyncPermissionsAsync(role.Id, request.Permissions, tenantId, cancellationToken);
            }

            await db.SaveChangesAsync([], true, cancellationToken);
            return new Response();
        }

        private async Task SyncPermissionsAsync(
            string roleId,
            IReadOnlyCollection<string> requestedPermissions,
            string tenantId,
            CancellationToken cancellationToken)
        {
            List<KrafterRoleClaim> permissions = await db.RoleClaims
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId &&
                            c.RoleId == roleId &&
                            c.ClaimType == KrafterClaims.Permission)
                .ToListAsync(cancellationToken);

            var permissionsToRemove = new List<KrafterRoleClaim>();
            var permissionsToUpdate = new List<KrafterRoleClaim>();
            var permissionsToAdd = new List<KrafterRoleClaim>();

            foreach (KrafterRoleClaim krafterRoleClaim in permissions)
            {
                if (krafterRoleClaim.ClaimValue is not null &&
                    !requestedPermissions.Contains(krafterRoleClaim.ClaimValue))
                {
                    krafterRoleClaim.IsDeleted = true;
                    permissionsToRemove.Add(krafterRoleClaim);
                }
            }

            foreach (KrafterRoleClaim krafterRoleClaim in permissions)
            {
                if (krafterRoleClaim.ClaimValue is not null &&
                    requestedPermissions.Contains(krafterRoleClaim.ClaimValue))
                {
                    krafterRoleClaim.IsDeleted = false;
                    permissionsToUpdate.Add(krafterRoleClaim);
                }
            }

            foreach (string claim in requestedPermissions)
            {
                KrafterRoleClaim? firstOrDefault = permissions.FirstOrDefault(c => c.ClaimValue == claim);
                if (firstOrDefault is null)
                {
                    permissionsToAdd.Add(new KrafterRoleClaim
                    {
                        RoleId = roleId, ClaimType = KrafterClaims.Permission, ClaimValue = claim
                    });
                }
            }

            if (permissionsToAdd.Count > 0)
            {
                db.RoleClaims.AddRange(permissionsToAdd);
            }

            if (permissionsToUpdate.Count > 0)
            {
                db.RoleClaims.UpdateRange(permissionsToUpdate);
            }

            if (permissionsToRemove.Count > 0)
            {
                db.RoleClaims.UpdateRange(permissionsToRemove);
            }
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder roleGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Roles)
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
                .MustHavePermission(KrafterAction.Update, KrafterResource.Roles);
        }
    }
}


