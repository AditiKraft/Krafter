using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using Refit;

namespace AditiKraft.Krafter.UI.Web.Infrastructure.Auth;

public static class TwoFactorBffEndpoints
{
    public static void MapTwoFactorBffEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup($"/{ApiRoutes.ApiPrefix}/{ApiRoutes.TwoFactor}");
        group.AddEndpointFilter(async (invocation, next) =>
        {
            HttpContext context = invocation.HttpContext;
            context.Response.Headers.CacheControl = "no-store";
            string origin = context.Request.Headers.Origin.ToString();
            if (HttpMethods.IsPost(context.Request.Method) && (context.Request.Headers["X-Krafter-Mfa"] != "1" ||
                (!string.IsNullOrEmpty(origin) && !string.Equals(origin, $"{context.Request.Scheme}://{context.Request.Host}", StringComparison.OrdinalIgnoreCase))))
            {
                return Results.Json(Response.Forbidden("Invalid request origin."), statusCode: 403);
            }

            try
            {
                return await next(invocation);
            }
            catch (ApiException ex)
            {
                // Return a generic envelope, never proxy arbitrary HTML or credential-bearing bodies.
                int status = (int)ex.StatusCode;
                if (status is >= 200 and < 300)
                {
                    status = 502;
                }

                return Results.Json(Response.CustomError("Verification failed. Check your code or sign in again.", status), statusCode: status);
            }
            catch (Polly.ExecutionRejectedException)
            {
                return Results.Json(Response.CustomError(ServerAuthApiErrors.UnavailableMessage, 503), statusCode: 503);
            }
        });
        group.MapPost("/login", async (TwoFactorRequest request, IAuthApiService service, CancellationToken cancellationToken) => Json(await service.CompleteTwoFactorAsync(request, cancellationToken)));
        group.MapGet("/status", async (ITwoFactorApi api, CancellationToken cancellationToken) => Json(await ServerAuthApiErrors.CallAsync(() => api.StatusAsync(cancellationToken), cancellationToken))).RequireAuthorization();
        group.MapPost("/setup", async (TwoFactorManagementRequest request, ITwoFactorApi api, CancellationToken cancellationToken) => Json(await ServerAuthApiErrors.CallAsync(() => api.SetupAsync(request, cancellationToken), cancellationToken))).RequireAuthorization();
        group.MapPost("/confirm", async (TwoFactorRequest request, ITwoFactorApi api, CancellationToken cancellationToken) => Json(await ServerAuthApiErrors.CallAsync(() => api.ConfirmAsync(request, cancellationToken), cancellationToken))).RequireAuthorization();
        group.MapPost("/recovery-codes", async (TwoFactorManagementRequest request, ITwoFactorApi api, CancellationToken cancellationToken) => Json(await ServerAuthApiErrors.CallAsync(() => api.RegenerateAsync(request, cancellationToken), cancellationToken))).RequireAuthorization();
        group.MapPost("/disable", async (TwoFactorManagementRequest request, ITwoFactorApi api, CancellationToken cancellationToken) =>
        {
            Response result = await ServerAuthApiErrors.CallAsync(() => api.DisableAsync(request, cancellationToken), cancellationToken);
            return Results.Json(result, statusCode: result.StatusCode);
        }).RequireAuthorization();
    }

    private static IResult Json<T>(Response<T> result) => Results.Json(result, statusCode: result.StatusCode);
}
