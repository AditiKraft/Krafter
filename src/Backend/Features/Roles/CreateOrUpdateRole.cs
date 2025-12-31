using Backend.Api;
using Backend.Api.Authorization;
using Backend.Common.Interfaces;
using Backend.Features.Roles._Shared;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using Krafter.Shared.Common;
using Krafter.Shared.Common.Auth;
using Krafter.Shared.Common.Auth.Permissions;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Roles;

public sealed class CreateOrUpdateRole
{
    public sealed class Handler(
        RoleManager<KrafterRole> roleManager,
        UserManager<KrafterUser> userManager,
        KrafterContext db,
        ITenantGetterService tenantGetterService) : IScopedHandler
    {
        public async Task<Response> CreateOrUpdateAsync(CreateOrUpdateRoleRequest request)
        {
            KrafterRole role;
            bool isNewRole = string.IsNullOrEmpty(request.Id);
            if (isNewRole)
            {
                role = new KrafterRole(request.Name, request.Description) { Id = Guid.NewGuid().ToString() };

                IdentityResult result = await roleManager.CreateAsync(role);
                if (!result.Succeeded)
                {
                    return Response.BadRequest($"Register role failed {result.Errors.ToString()}");
                }
            }
            else
            {
                role = await roleManager.FindByIdAsync(request.Id);
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
            }

            if (request.Permissions is { Count: > 0 })
            {
                if (role.Name == KrafterRoleConstant.Admin)
                {
                    return Response.BadRequest("Not allowed to modify Permissions for this Role.");
                }

                List<KrafterRoleClaim> permissions = await db.RoleClaims
                    .IgnoreQueryFilters()
                    .Where(c => c.TenantId == tenantGetterService.Tenant.Id && c.RoleId == request.Id &&
                                c.ClaimType == KrafterClaims.Permission)
                    .ToListAsync();

                var permissionsToRemove = new List<KrafterRoleClaim>();
                var permissionsToUpdate = new List<KrafterRoleClaim>();
                var permissionsToAdd = new List<KrafterRoleClaim>();

                foreach (KrafterRoleClaim krafterRoleClaim in permissions)
                {
                    if (krafterRoleClaim.ClaimValue is not null &&
                        !request.Permissions.Contains(krafterRoleClaim.ClaimValue))
                    {
                        krafterRoleClaim.IsDeleted = true;
                        permissionsToRemove.Add(krafterRoleClaim);
                    }
                }

                foreach (KrafterRoleClaim krafterRoleClaim in permissions)
                {
                    if (krafterRoleClaim.ClaimValue is not null &&
                        request.Permissions.Contains(krafterRoleClaim.ClaimValue))
                    {
                        krafterRoleClaim.IsDeleted = false;
                        permissionsToUpdate.Add(krafterRoleClaim);
                    }
                }

                foreach (string claim in request.Permissions)
                {
                    KrafterRoleClaim? firstOrDefault = permissions.FirstOrDefault(c => c.ClaimValue == claim);
                    if (firstOrDefault is null)
                    {
                        permissionsToAdd.Add(new KrafterRoleClaim
                        {
                            RoleId = role.Id, ClaimType = KrafterClaims.Permission, ClaimValue = claim
                        });
                    }
                }

                db.RoleClaims.AddRange(permissionsToAdd);
                db.RoleClaims.UpdateRange(permissionsToUpdate);
                db.RoleClaims.UpdateRange(permissionsToRemove);
            }


            await db.SaveChangesAsync(new List<string>());
            return new Response();
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder roleGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Roles)
                .AddFluentValidationFilter();

            roleGroup.MapPost("/create-or-update", async
                ([FromBody] CreateOrUpdateRoleRequest request,
                    [FromServices] Handler roleService) =>
                {
                    Response res = await roleService.CreateOrUpdateAsync(request);
                    return TypedResults.Ok(res);
                })
                .Produces<Response>()
                .MustHavePermission(KrafterAction.Create, KrafterResource.Roles);
        }
    }
}
