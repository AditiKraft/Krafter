using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.Backend.Features.Auth;

public sealed class Login
{
    internal sealed class Handler(TwoFactorService twoFactor) : IScopedHandler
    {
        public Task<Response<LoginResponse>> LoginAsync(TokenRequest request, string ipAddress, CancellationToken cancellationToken) =>
            twoFactor.PasswordLoginAsync(request, ipAddress);
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGroup(ApiRoutes.Tokens).AddFluentValidationFilter().MapPost("/", async
                (TokenRequest request, HttpContext context, Handler handler) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                Response<LoginResponse> result = await handler.LoginAsync(request,
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown", context.RequestAborted);
                return Results.Json(result, statusCode: result.StatusCode);
            }).RequireRateLimiting("authentication").Produces<Response<LoginResponse>>();
        }
    }
}
