using System.Security.Claims;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Common;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public static class TwoFactorEndpoints
{
    public static RouteGroupBuilder MapTwoFactorGroup(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(ApiRoutes.TwoFactor)
            .AddFluentValidationFilter()
            .RequireRateLimiting("authentication");
        group.AddEndpointFilter(async (invocation, next) =>
        {
            HttpContext context = invocation.HttpContext;
            context.Response.Headers.CacheControl = "no-store";
            // Non-simple header and exact origin protect cookie-authenticated JSON operations.
            if (HttpMethods.IsPost(context.Request.Method) && !IsSafeRequest(context))
            {
                return Results.Json(Response.Forbidden("Invalid request origin."), statusCode: 403);
            }
            return await next(invocation);
        });
        return group;
    }

    public static bool IsSafeRequest(HttpContext context)
    {
        if (context.Request.Headers["X-Krafter-Mfa"] != "1")
        {
            return false;
        }
        string origin = context.Request.Headers.Origin.ToString();
        return string.IsNullOrEmpty(origin) || string.Equals(origin,
            $"{context.Request.Scheme}://{context.Request.Host}", StringComparison.OrdinalIgnoreCase);
    }

    public static string UserId(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}
