using Backend.Common.Interfaces.Auth;

namespace Backend.Api.Middleware;

public class CurrentUserMiddleware(ICurrentUserInitializer currentUserInitializer) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        currentUserInitializer.SetCurrentUser(context.User);

        await next(context);
    }
}

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseCurrentUser(this IApplicationBuilder app) =>
        app.UseMiddleware<CurrentUserMiddleware>();

    public static IApplicationBuilder AuthMiddleware(this IApplicationBuilder builder, IConfiguration config)
    {
        return builder
            .UseAuthentication()
            .UseCurrentUser()
            .UseAuthorization();
    }
}
