using Backend.Api;
using Backend.Common;
using Backend.Common.Models;
using Backend.Features.Users._Shared;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Features.Users;

public sealed class ResetPassword
{
    public sealed class ResetPasswordRequest
    {
        public string? Email { get; set; }
        public string? Token { get; set; }
        public string? Password { get; set; }
    }

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

    internal sealed class Validator : AbstractValidator<ResetPasswordRequest>
    {
        public Validator()
        {
            RuleFor(p => p.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format");

            RuleFor(p => p.Token)
                .NotEmpty().WithMessage("Reset token is required");

            RuleFor(p => p.Password)
                .NotEmpty().WithMessage("New password is required")
                .MinimumLength(6).WithMessage("Password must be at least 6 characters");
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Users)
                .AddFluentValidationFilter();

            userGroup.MapPost("/reset-password", async (
                    [FromBody] ResetPasswordRequest request,
                    [FromServices] Handler handler) =>
                {
                    Response res = await handler.ResetPasswordAsync(request);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                ;
        }
    }
}
