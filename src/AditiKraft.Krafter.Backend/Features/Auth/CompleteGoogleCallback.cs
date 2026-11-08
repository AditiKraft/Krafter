using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.Backend.Features.Auth;

public sealed class CompleteGoogleCallback
{
    internal sealed class Handler(GoogleOAuthFlow flow) : IScopedHandler
    {
        public Task<Response<GoogleLoginCallbackResponse>> CompleteAsync(GoogleLoginCallbackRequest request, CancellationToken cancellationToken) =>
            flow.CallbackAsync(request, cancellationToken);
    }
    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpoints) => endpoints.MapPost($"{ApiRoutes.ExternalAuth}/google/callback",
            async (GoogleLoginCallbackRequest request, Handler handler, HttpContext context, CancellationToken cancellationToken) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                Response<GoogleLoginCallbackResponse> result = await handler.CompleteAsync(request, cancellationToken);
                return Results.Json(result, statusCode: result.StatusCode);
            }).AllowAnonymous().RequireRateLimiting("authentication");
    }
}
