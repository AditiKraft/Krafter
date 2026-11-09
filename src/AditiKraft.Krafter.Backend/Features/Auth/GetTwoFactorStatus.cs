using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.Backend.Features.Auth;

public sealed class GetTwoFactorStatus
{
    internal sealed class Handler(TwoFactorService twoFactor) : IScopedHandler
    {
        public Task<Response<TwoFactorStatusResponse>> GetAsync(
            string userId, CancellationToken cancellationToken) =>
            twoFactor.StatusAsync(userId, cancellationToken);
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapTwoFactorGroup().MapGet("/status", async (
                HttpContext context, Handler handler, CancellationToken cancellationToken) =>
            {
                Response<TwoFactorStatusResponse> result = await handler.GetAsync(TwoFactorEndpoints.UserId(context), cancellationToken);
                return Results.Json(result, statusCode: result.StatusCode);
            })
                .RequireAuthorization()
                .Produces<Response<TwoFactorStatusResponse>>();
        }
    }
}
