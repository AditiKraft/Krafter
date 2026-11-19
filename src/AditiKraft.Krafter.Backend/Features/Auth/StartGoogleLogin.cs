using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.Backend.Features.Auth;

public sealed class StartGoogleLogin
{
    internal sealed class Handler(GoogleOAuthFlow flow) : IScopedHandler
    {
        public Task<Response<GoogleLoginStartResponse>> StartAsync(GoogleLoginStartRequest request, CancellationToken cancellationToken) =>
            flow.StartAsync(request, cancellationToken);
    }
    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpoints) => endpoints.MapPost($"{ApiRoutes.ExternalAuth}/google/start",
            async (GoogleLoginStartRequest request, Handler handler, HttpContext context, CancellationToken cancellationToken) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                Response<GoogleLoginStartResponse> result = await handler.StartAsync(request, cancellationToken);
                return Results.Json(result, statusCode: result.StatusCode);
            }).AllowAnonymous().RequireRateLimiting("authentication");
    }
}
