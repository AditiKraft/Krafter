using System.Security.Claims;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.Backend.Features.Auth;

public sealed class TwoFactor : IRouteRegistrar
{
    public void MapRoute(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(ApiRoutes.TwoFactor).AddFluentValidationFilter().RequireRateLimiting("authentication");
        group.AddEndpointFilter(async (invocation, next) =>
        {
            HttpContext context = invocation.HttpContext;
            context.Response.Headers.CacheControl = "no-store";
            // Non-simple header + exact browser origin checking protects cookie-authenticated JSON operations.
            if (HttpMethods.IsPost(context.Request.Method) && !IsSafeRequest(context))
                return Results.Json(Response.Forbidden("Invalid request origin."), statusCode: 403);
            return await next(invocation);
        });
        group.MapPost("/login", async (TwoFactorRequest request, TwoFactorService service, HttpContext context) =>
            Json(await service.CompleteLoginAsync(request, context.Connection.RemoteIpAddress?.ToString() ?? "unknown"))).AllowAnonymous().Produces<Response<TokenResponse>>();
        group.MapGet("/status", async (TwoFactorService service, HttpContext context) => Json(await service.StatusAsync(UserId(context)))).RequireAuthorization().Produces<Response<TwoFactorStatusResponse>>();
        group.MapPost("/setup", async (TwoFactorManagementRequest request, TwoFactorService service, HttpContext context) =>
            Json(await service.BeginEnrollmentAsync(UserId(context), request, context.User))).RequireAuthorization().Produces<Response<TwoFactorSetupResponse>>();
        group.MapPost("/confirm", async (TwoFactorRequest request, TwoFactorService service, HttpContext context) =>
            Json(await service.ConfirmEnrollmentAsync(UserId(context), request))).RequireAuthorization().Produces<Response<RecoveryCodesResponse>>();
        group.MapPost("/disable", async (TwoFactorManagementRequest request, TwoFactorService service, HttpContext context) =>
        {
            Response result = await service.DisableAsync(UserId(context), request, context.User);
            return Results.Json(result, statusCode: result.StatusCode);
        }).RequireAuthorization();
        group.MapPost("/recovery-codes", async (TwoFactorManagementRequest request, TwoFactorService service, HttpContext context) =>
            Json(await service.RegenerateAsync(UserId(context), request, context.User))).RequireAuthorization().Produces<Response<RecoveryCodesResponse>>();
        endpoints.MapGet($"{ApiRoutes.Tokens}/validate", async (SessionValidator validator, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return await validator.ValidateAsync(context.User)
                ? Results.Json(Response<bool>.Success(true))
                : Results.Json(Response<bool>.Unauthorized("Session expired. Sign in again."), statusCode: 401);
        }).RequireAuthorization();
    }

    public static bool IsSafeRequest(HttpContext context)
    {
        if (context.Request.Headers["X-Krafter-Mfa"] != "1") return false;
        string origin = context.Request.Headers.Origin.ToString();
        return string.IsNullOrEmpty(origin) || string.Equals(origin,
            $"{context.Request.Scheme}://{context.Request.Host}", StringComparison.OrdinalIgnoreCase);
    }

    private static string UserId(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private static IResult Json<T>(Response<T> result) => Results.Json(result, statusCode: result.StatusCode);
}
