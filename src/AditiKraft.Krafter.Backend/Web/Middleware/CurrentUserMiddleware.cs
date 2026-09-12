using AditiKraft.Krafter.Backend.Common.Auth;

namespace AditiKraft.Krafter.Backend.Web.Middleware;

public class CurrentUserMiddleware(ICurrentUserInitializer currentUserInitializer) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        currentUserInitializer.SetCurrentUser(context.User);

        await next(context);
    }
}
