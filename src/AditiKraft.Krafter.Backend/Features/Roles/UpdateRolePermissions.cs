using System.Security.Claims;
using AditiKraft.Krafter.Backend.Api;
using AditiKraft.Krafter.Backend.Features.Roles._Shared;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Api.Authorization;
using AditiKraft.Krafter.Shared.Common;
using AditiKraft.Krafter.Shared.Common.Auth;
using AditiKraft.Krafter.Shared.Common.Auth.Permissions;
using AditiKraft.Krafter.Shared.Common.Models;
using AditiKraft.Krafter.Shared.Contracts.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AditiKraft.Krafter.Backend.Features.Roles;

public sealed class UpdateRolePermissions
{
    internal sealed class Handler(
        RoleManager<KrafterRole> roleManager,
        KrafterContext db) : IScopedHandler
    {
        public async Task<Response> UpdatePermissionsAsync(
            UpdateRolePermissionsRequest request,
            CancellationToken cancellationToken)
        {
            KrafterRole? role = await roleManager.FindByIdAsync(request.RoleId);

            if (role is null)
            {
                return new Response { IsError = true, StatusCode = 404, Message = "Role Not Found" };
            }

            if (role.Name == KrafterRoleConstant.Admin)
            {
                return new Response
                {
                    IsError = true, StatusCode = 403, Message = "Not allowed to modify Permissions for this Role."
                };
            }

            IList<Claim> currentClaims = await roleManager.GetClaimsAsync(role);

            // Remove permissions that were previously selected
            foreach (Claim claim in currentClaims.Where(c => request.Permissions.All(p => p != c.Value)))
            {
                IdentityResult removeResult = await roleManager.RemoveClaimAsync(role, claim);
                if (!removeResult.Succeeded)
                {
                    return new Response
                    {
                        IsError = true,
                        StatusCode = 400,
                        Message =
                            $"Update permissions failed: {string.Join(", ", removeResult.Errors.Select(e => e.Description))}"
                    };
                }
            }

            // Add all permissions that were not previously selected
            foreach (string permission in request.Permissions.Where(c => currentClaims.All(p => p.Value != c)))
            {
                if (!string.IsNullOrEmpty(permission))
                {
                    db.RoleClaims.Add(new KrafterRoleClaim
                    {
                        RoleId = role.Id, ClaimType = KrafterClaims.Permission, ClaimValue = permission
                    });
                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            return new Response { Message = "Role permissions updated successfully" };
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder roleGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Roles)
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
                .MustHavePermission(KrafterAction.Update, KrafterResource.Roles);
        }
    }
}
