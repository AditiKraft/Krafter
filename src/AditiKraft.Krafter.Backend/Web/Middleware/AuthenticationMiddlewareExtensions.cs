namespace AditiKraft.Krafter.Backend.Web.Middleware;

public static class AuthenticationMiddlewareExtensions
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
