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
        public async Task<Response> ResetPasswordAsync(ResetPasswordRequest request)
        {
            ApplicationUser? user = await userManager.FindByEmailAsync(request.Email?.Normalize()!);
            if (user is null)
            {
                return new Response
                {
                    Message = "If the email is registered, you will receive a password reset link.",
                    StatusCode = 200,
                    IsError = false
                };
            }

            // Identity saves the password, security stamp and session version in one update.
            user.AuthenticationVersion++;
            IdentityResult result = await userManager.ResetPasswordAsync(user, request.Token!, request.Password!);
            if (!result.Succeeded)
            {
                db.Entry(user).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                return new Response
                {
                    Message = "An error occurred while resetting password", StatusCode = 400, IsError = true
                };
            }

            return new Response();
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
                    [FromServices] Handler handler) =>
                {
                    Response res = await handler.ResetPasswordAsync(request);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>();
        }
    }
}



