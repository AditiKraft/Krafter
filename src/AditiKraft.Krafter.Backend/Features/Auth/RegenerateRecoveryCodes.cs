using System.Security.Claims;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.Backend.Features.Auth;

public sealed class RegenerateRecoveryCodes
{
    internal sealed class Handler(TwoFactorService twoFactor) : IScopedHandler
    {
        public Task<Response<RecoveryCodesResponse>> RegenerateAsync(
            string userId, TwoFactorManagementRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
            twoFactor.RegenerateAsync(userId, request, principal, cancellationToken);
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapTwoFactorGroup().MapPost("/recovery-codes", async (
                TwoFactorManagementRequest request, HttpContext context, Handler handler, CancellationToken cancellationToken) =>
            {
                Response<RecoveryCodesResponse> result = await handler.RegenerateAsync(TwoFactorEndpoints.UserId(context), request, context.User, cancellationToken);
                return Results.Json(result, statusCode: result.StatusCode);
            })
                .RequireAuthorization()
                .Produces<Response<RecoveryCodesResponse>>();
        }
    }
}
