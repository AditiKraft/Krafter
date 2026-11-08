using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AditiKraft.Krafter.Backend.Features.Users;

public sealed class ResetPassword
{
    internal sealed class Handler(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : IScopedHandler
    {
        public async Task<Response> ResetPasswordAsync(ResetPasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplicationUser? user = await userManager.FindByEmailAsync(request.Email?.Normalize()!);
            cancellationToken.ThrowIfCancellationRequested();
            const string message = "The password reset link is invalid or expired. Request a new link and try again.";
            if (user is not { IsActive: true, IsDeleted: false })
            {
                return Response.BadRequest(message);
            }

            // Identity saves the password, security stamp and session version in one update.
            user.AuthenticationVersion++;
            IdentityResult result = await userManager.ResetPasswordAsync(user, request.Token!, request.Password!);
            if (!result.Succeeded)
            {
                db.Entry(user).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                cancellationToken.ThrowIfCancellationRequested();
                return Response.BadRequest(message);
            }

            // Identity has committed the password and version; cancellation cannot undo that update.
            return Response.Success();
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(ApiRoutes.Users)
                .AddFluentValidationFilter();

            userGroup.MapPost($"/{RouteSegment.ResetPassword}", async (
                    [FromBody] ResetPasswordRequest request,
                    [FromServices] Handler handler,
                    CancellationToken cancellationToken) =>
                {
                    Response res = await handler.ResetPasswordAsync(request, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .RequireRateLimiting("authentication");
        }
    }
}



