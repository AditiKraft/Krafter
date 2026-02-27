using AditiKraft.Krafter.Backend.Api;
using AditiKraft.Krafter.Backend.Api.Authorization;
using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Features.Tenants._Shared;
using AditiKraft.Krafter.Backend.Features.Users._Shared;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Users;

public sealed class UpdateUser
{
    internal sealed class Handler(
        UserManager<KrafterUser> userManager,
        ITenantGetterService tenantGetterService,
        TenantDbContext tenantDbContext,
        KrafterContext db) : IScopedHandler
    {
        public async Task<Response> UpdateAsync(string id, CreateUserRequest request, CancellationToken cancellationToken)
        {
            request.Id = id;
            KrafterUser? user = await userManager.FindByIdAsync(id);
            if (user is null)
            {
                return Response.NotFound("User Not Found");
            }

            if (request.FirstName != user.FirstName)
            {
                user.FirstName = request.FirstName;
            }

            if (request.LastName != user.LastName)
            {
                user.LastName = request.LastName;
            }

            if (request.PhoneNumber != user.PhoneNumber)
            {
                user.PhoneNumber = request.PhoneNumber;
            }

            if (request.Email != user.Email)
            {
                if (request.UpdateTenantEmail)
                {
                    Tenant? tenant = await tenantDbContext.Tenants
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.AdminEmail == user.Email, cancellationToken);

                    if (tenant is not null)
                    {
                        tenant.AdminEmail = request.Email;
                        tenantDbContext.Tenants.Update(tenant);
                    }
                }

                user.Email = request.Email;
                user.UserName = request.Email;
            }

            IdentityResult result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return Response.BadRequest(
                    $"Update profile failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            if (request.Roles?.Any() == true)
            {
                await SyncRolesAsync(user.Id, request.Roles, cancellationToken);
            }

            await db.SaveChangesAsync([], true, cancellationToken);
            await tenantDbContext.SaveChangesAsync(cancellationToken);

            return new Response();
        }

        private async Task SyncRolesAsync(string userId, IReadOnlyCollection<string> requestedRoles,
            CancellationToken cancellationToken)
        {
            List<KrafterUserRole> existingRoles = await db.UserRoles
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantGetterService.Tenant.Id && c.UserId == userId)
                .ToListAsync(cancellationToken);

            var rolesToRemove = existingRoles.Where(r => !requestedRoles.Contains(r.RoleId)).ToList();
            var rolesToUpdate = existingRoles.Where(r => requestedRoles.Contains(r.RoleId)).ToList();
            var rolesToAdd = requestedRoles
                .Where(roleId => !existingRoles.Any(er => er.RoleId == roleId))
                .Select(roleId => new KrafterUserRole { RoleId = roleId, UserId = userId })
                .ToList();

            foreach (KrafterUserRole role in rolesToRemove)
            {
                role.IsDeleted = true;
            }

            foreach (KrafterUserRole role in rolesToUpdate)
            {
                role.IsDeleted = false;
            }

            if (rolesToAdd.Count > 0)
            {
                db.UserRoles.AddRange(rolesToAdd);
            }

            if (rolesToRemove.Count > 0)
            {
                db.UserRoles.UpdateRange(rolesToRemove);
            }

            if (rolesToUpdate.Count > 0)
            {
                db.UserRoles.UpdateRange(rolesToUpdate);
            }
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Users)
                .AddFluentValidationFilter();

            userGroup.MapPut($"/{RouteSegment.ById}", async (
                    [FromRoute] string id,
                    [FromBody] CreateUserRequest request,
                    [FromServices] Handler handler,
                    CancellationToken cancellationToken) =>
                {
                    Response res = await handler.UpdateAsync(id, request, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .MustHavePermission(KrafterAction.Update, KrafterResource.Users);
        }
    }
}
