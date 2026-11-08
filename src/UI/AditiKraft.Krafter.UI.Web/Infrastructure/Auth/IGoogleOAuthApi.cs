using Refit;

namespace AditiKraft.Krafter.UI.Web.Infrastructure.Auth;

// Server-only transport. Browser callback correlation never travels through WebAssembly.
public interface IGoogleOAuthApi
{
    [Post("/api/external-auth/google/start")]
    Task<Response<GoogleLoginStartResponse>> StartAsync([Body] GoogleLoginStartRequest request, CancellationToken cancellationToken);
    [Post("/api/external-auth/google/callback")]
    Task<Response<GoogleLoginCallbackResponse>> CallbackAsync([Body] GoogleLoginCallbackRequest request, CancellationToken cancellationToken);
}
