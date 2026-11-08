using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

namespace AditiKraft.Krafter.UI.Web.Infrastructure.Auth;

public static class AuthTokenBffEndpoints
{
    // Split-host primary authentication proxies to the backend. Single-host maps its backend operations directly.
    public static void MapAuthTokenBffEndpoints(this WebApplication app)
    {
        app.MapClientSessionEndpoints();
        app.MapPost($"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}", async (
            TokenRequest request, IAuthApiService api, CancellationToken cancellationToken) =>
        {
            Response<LoginResponse> result = await api.CreateTokenAsync(request, cancellationToken);
            return Results.Json(result, statusCode: result.StatusCode);
        });
        app.MapPost($"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}/{RouteSegment.Refresh}", async (
            RefreshTokenRequest request, IAuthApiService api, CancellationToken cancellationToken) =>
        {
            Response<TokenResponse> result = await api.RefreshTokenAsync(request, cancellationToken);
            return Results.Json(result, statusCode: result.StatusCode);
        });
        app.MapPost($"/{ApiRoutes.ApiPrefix}/{ApiRoutes.ExternalAuth}/{RouteSegment.Google}", async (
            TokenRequest request, IAuthApiService api, CancellationToken cancellationToken) =>
        {
            Response<LoginResponse> result = await api.ExternalAuthAsync(request, cancellationToken);
            return Results.Json(result, statusCode: result.StatusCode);
        });
    }

    // Both hosts keep browser session storage behind these server-only adapters.
    public static void MapClientSessionEndpoints(this WebApplication app)
    {
        app.MapGet($"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}/{RouteSegment.Current}", async (
            IAuthApiService api, CancellationToken cancellationToken) =>
        {
            Response<TokenResponse> result = await api.GetCurrentTokenAsync(cancellationToken);
            return Results.Json(result, statusCode: result.StatusCode);
        }).RequireAuthorization();
        app.MapPost($"/{ApiRoutes.ApiPrefix}/{ApiRoutes.Tokens}/{RouteSegment.Logout}", async (
            IAuthApiService api, CancellationToken cancellationToken) =>
        {
            await api.LogoutAsync(cancellationToken);
            return Results.Ok();
        });
    }
}
