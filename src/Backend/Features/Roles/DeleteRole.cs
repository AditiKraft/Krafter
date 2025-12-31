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

public sealed class DeleteRole
{
    internal sealed class Handler(
        RoleManager<KrafterRole> roleManager,
        UserManager<KrafterUser> userManager,
        KrafterContext db,
        ITenantGetterService tenantGetterService) : IScopedHandler
    {
        public async Task<Response> DeleteAsync(DeleteRequestInput requestInput)
        {
            KrafterRole? role = await roleManager.FindByIdAsync(requestInput.Id);

            if (role is null)
            {
                return Response.NotFound("Role Not Found");
            }

            if (KrafterRoleConstant.IsDefault(role.Name!))
            {
                return Response.Forbidden($"Not allowed to delete {role.Name} Role.");
            }

            role.IsDeleted = true;
            role.DeleteReason = requestInput.DeleteReason;
            db.Roles.Update(role);

            List<KrafterRoleClaim> krafterRoleClaims = await db.RoleClaims
                .Where(c => c.RoleId == requestInput.Id &&
                            c.ClaimType == KrafterClaims.Permission)
                .ToListAsync();
            foreach (KrafterRoleClaim krafterRoleClaim in krafterRoleClaims)
            {
                krafterRoleClaim.IsDeleted = true;
            }

            await db.SaveChangesAsync([nameof(KrafterRole)]);
            return new Response();
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder roleGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Roles)
                .AddFluentValidationFilter();

            roleGroup.MapPost("/delete", async
                ([FromBody] DeleteRequestInput roleRequestInput,
                    [FromServices] Handler roleService) =>
                {
                    Response res = await roleService.DeleteAsync(roleRequestInput);
                    return TypedResults.Ok(res);
                })
                .Produces<Response>()
                .MustHavePermission(KrafterAction.Delete, KrafterResource.Roles);
        }
    }
}
