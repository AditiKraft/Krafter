using AditiKraft.Krafter.Backend.Api;
using AditiKraft.Krafter.Backend.Features.Users._Shared;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AditiKraft.Krafter.Backend.Features.Users;

public sealed class ResetPassword
{
    internal sealed class Handler(UserManager<KrafterUser> userManager) : IScopedHandler
    {
        public async Task<Response> ResetPasswordAsync(ResetPasswordRequest request)
        {
            KrafterUser? user = await userManager.FindByEmailAsync(request.Email?.Normalize()!);
            if (user is null)
            {
                return new Response
                {
                    Message = "If the email is registered, you will receive a password reset link.",
                    StatusCode = 200,
                    IsError = false
                };
            }

            IdentityResult result = await userManager.ResetPasswordAsync(user, request.Token!, request.Password!);
            if (!result.Succeeded)
            {
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
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Users)
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
