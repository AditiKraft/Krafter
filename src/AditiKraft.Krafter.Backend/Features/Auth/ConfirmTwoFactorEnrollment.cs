using System.Security.Claims;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.Backend.Features.Auth;

public sealed class ConfirmTwoFactorEnrollment
{
    internal sealed class Handler(TwoFactorService twoFactor) : IScopedHandler
    {
        public Task<Response<RecoveryCodesResponse>> ConfirmAsync(
            string userId, TwoFactorRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
            twoFactor.ConfirmEnrollmentAsync(userId, request, principal, cancellationToken);
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapTwoFactorGroup().MapPost("/confirm", async (
                TwoFactorRequest request, HttpContext context, Handler handler, CancellationToken cancellationToken) =>
            {
                Response<RecoveryCodesResponse> result = await handler.ConfirmAsync(TwoFactorEndpoints.UserId(context), request, context.User, cancellationToken);
                return Results.Json(result, statusCode: result.StatusCode);
            })
                .RequireAuthorization()
                .Produces<Response<RecoveryCodesResponse>>();
        }
    }
}
