using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using Microsoft.AspNetCore.Mvc;

namespace AditiKraft.Krafter.Backend.Features.Users;

public sealed class UpdateUser
{
    internal sealed class Handler(IUserMutationService service) : IScopedHandler
    {
        public Task<Response> UpdateAsync(string id, CreateUserRequest request, CancellationToken cancellationToken)
            => service.UpdateAsync(id, request, cancellationToken);
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(ApiRoutes.Users)
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
                .MustHavePermission(PermissionAction.Update, PermissionResource.Users);
        }
    }
}
