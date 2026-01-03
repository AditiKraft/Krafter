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
        public async Task<Response> DeleteAsync(string id)
        {
            KrafterRole? role = await roleManager.FindByIdAsync(id);

            if (role is null)
            {
                return Response.NotFound("Role Not Found");
            }

            if (KrafterRoleConstant.IsDefault(role.Name!))
            {
                return Response.Forbidden($"Not allowed to delete {role.Name} Role.");
            }

            role.IsDeleted = true;
            db.Roles.Update(role);

            List<KrafterRoleClaim> krafterRoleClaims = await db.RoleClaims
                .Where(c => c.RoleId == id &&
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

            roleGroup.MapDelete($"/{RouteSegment.ById}", async
                ([FromRoute] string id,
                    [FromServices] Handler handler) =>
                {
                    Response res = await handler.DeleteAsync(id);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .MustHavePermission(KrafterAction.Delete, KrafterResource.Roles);
        }
    }
}
