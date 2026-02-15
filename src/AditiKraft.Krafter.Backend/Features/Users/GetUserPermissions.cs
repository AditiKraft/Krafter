using AditiKraft.Krafter.Backend.Api;
using AditiKraft.Krafter.Backend.Common.Interfaces.Auth;
using AditiKraft.Krafter.Backend.Features.Roles._Shared;
using AditiKraft.Krafter.Backend.Features.Users._Shared;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Common;
using AditiKraft.Krafter.Shared.Common;
using AditiKraft.Krafter.Shared.Common.Auth;
using AditiKraft.Krafter.Shared.Common.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Users;

public sealed class GetUserPermissions
{
    internal sealed class Handler(
        UserManager<KrafterUser> userManager,
        RoleManager<KrafterRole> roleManager,
        KrafterContext db) : IScopedHandler
    {
        public async Task<Response<List<string>>> GetPermissionsAsync(string userId,
            CancellationToken cancellationToken)
        {
            KrafterUser? user = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == userId, cancellationToken);

            if (user is null)
            {
                return new Response<List<string>> { IsError = true, Message = "User Not Found", StatusCode = 404 };
            }

            IList<string> userRoles = await userManager.GetRolesAsync(user);
            var permissions = new List<string>();

            foreach (KrafterRole role in await roleManager.Roles.AsNoTracking()
                         .Where(r => userRoles.Contains(r.Name!) && r.IsDeleted == false)
                         .ToListAsync(cancellationToken))
            {
                permissions.AddRange(await db.RoleClaims.AsNoTracking()
                    .Where(rc =>
                        rc.RoleId == role.Id &&
                        rc.ClaimType == KrafterClaims.Permission &&
                        rc.IsDeleted == false)
                    .Select(rc => rc.ClaimValue!)
                    .ToListAsync(cancellationToken));
            }

            return new Response<List<string>> { Data = permissions.Distinct().ToList() };
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Users)
                .AddFluentValidationFilter();

            userGroup.MapGet($"/{RouteSegment.Permissions}", async (
                    [FromServices] Handler handler,
                    [FromServices] ICurrentUser currentUser) =>
                {
                    Response<List<string>> res =
                        await handler.GetPermissionsAsync(currentUser.GetUserId(), CancellationToken.None);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<List<string>>>()
                .RequireAuthorization();
        }
    }
}
