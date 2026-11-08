using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace AditiKraft.Krafter.Backend.Features.Users;

public sealed class GetUserPermissions
{
    internal sealed class Handler(IUserService userService) : IScopedHandler
    {
        public Task<Response<List<string>>> GetPermissionsAsync(string userId,
            CancellationToken cancellationToken)
            => userService.GetPermissionsAsync(userId, cancellationToken);
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(ApiRoutes.Users)
                .AddFluentValidationFilter();

            userGroup.MapGet($"/{RouteSegment.Permissions}", async (
                    [FromServices] Handler handler,
                    [FromServices] ICurrentUser currentUser,
                    CancellationToken cancellationToken) =>
                {
                    Response<List<string>> res =
                        await handler.GetPermissionsAsync(currentUser.GetUserId(), cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<List<string>>>()
                .RequireAuthorization();
        }
    }
}
