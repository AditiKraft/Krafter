using AditiKraft.Krafter.Backend.Api;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Api.Authorization;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Roles;

public sealed class DeleteRole
{
    internal sealed class Handler(
        RoleManager<KrafterRole> roleManager,
        KrafterContext db) : IScopedHandler
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


