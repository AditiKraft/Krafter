using System.Security.Claims;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Common;

namespace AditiKraft.Krafter.Backend.Features.Auth;

public sealed class ValidateSession
{
    internal sealed class Handler(SessionValidator sessions) : IScopedHandler
    {
        public async Task<Response<bool>> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken) =>
            await sessions.ValidateAsync(principal, cancellationToken)
                ? Response<bool>.Success(true)
                : Response<bool>.Unauthorized("Session expired. Sign in again.");
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet($"{ApiRoutes.Tokens}/validate", async (
                HttpContext context, Handler handler, CancellationToken cancellationToken) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                Response<bool> result = await handler.ValidateAsync(context.User, cancellationToken);
                return Results.Json(result, statusCode: result.StatusCode);
            }).RequireAuthorization().Produces<Response<bool>>();
        }
    }
}
