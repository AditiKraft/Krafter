using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.Backend.Features.Auth;

public sealed class CompleteTwoFactorLogin
{
    internal sealed class Handler(TwoFactorService twoFactor) : IScopedHandler
    {
        public Task<Response<TokenResponse>> CompleteAsync(
            TwoFactorRequest request, string ipAddress, CancellationToken cancellationToken) =>
            twoFactor.CompleteLoginAsync(request, ipAddress, cancellationToken);
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapTwoFactorGroup().MapPost("/login", async (
                TwoFactorRequest request, HttpContext context, Handler handler, CancellationToken cancellationToken) =>
            {
                Response<TokenResponse> result = await handler.CompleteAsync(request, context.Connection.RemoteIpAddress?.ToString() ?? "unknown", cancellationToken);
                return Results.Json(result, statusCode: result.StatusCode);
            })
                .AllowAnonymous()
                .Produces<Response<TokenResponse>>();
        }
    }
}
