using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Users;

public sealed class DeleteUser
{
    internal sealed class Handler(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db) : IScopedHandler
    {
        public async Task<Response> DeleteAsync(string id)
        {
            ApplicationUser? user = await userManager.FindByIdAsync(id);
            if (user is null)
            {
                return new Response { IsError = true, Message = "User Not Found", StatusCode = 404 };
            }

            if (user.IsOwner)
            {
                return new Response { IsError = true, Message = "Owner cannot be deleted", StatusCode = 403 };
            }

            user.IsDeleted = true;
            db.Users.Update(user);

            List<ApplicationUserRole> userRoles = await db.UserRoles
                .Where(c => c.UserId == id)
                .ToListAsync();

            foreach (ApplicationUserRole userRole in userRoles)
            {
                userRole.IsDeleted = true;
            }

            await db.SaveChangesAsync([nameof(ApplicationUser)]);

            return new Response();
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(ApiRoutes.Users)
                .AddFluentValidationFilter();

            userGroup.MapDelete($"/{RouteSegment.ById}", async (
                    [FromRoute] string id,
                    [FromServices] Handler handler) =>
                {
                    Response res = await handler.DeleteAsync(id);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .MustHavePermission(PermissionAction.Delete, PermissionResource.Users);
        }
    }
}



