using Backend.Api;
using Backend.Api.Authorization;
using Backend.Application.BackgroundJobs;
using Backend.Application.Notifications;
using Backend.Common;
using Backend.Common.Auth.Permissions;
using Backend.Common.Interfaces;
using Backend.Common.Interfaces.Auth;
using Backend.Common.Models;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Features.Users;

public sealed class ChangePassword
{
    public sealed class ChangePasswordRequest
    {
        public string Password { get; set; } = default!;
        public string NewPassword { get; set; } = default!;
        public string ConfirmNewPassword { get; set; } = default!;
    }

    internal sealed class Handler(
        UserManager<KrafterUser> userManager,
        ICurrentUser currentUser,
        ITenantGetterService tenantGetterService,
        IJobService jobService) : IScopedHandler
    {
        public async Task<Response> ChangePasswordAsync(ChangePasswordRequest request)
        {
            KrafterUser? user = await userManager.FindByIdAsync(currentUser.GetUserId());
            if (user is null)
            {
                return new Response { IsError = true, Message = "User Not Found", StatusCode = 404 };
            }

            IdentityResult result = await userManager.ChangePasswordAsync(user, request.Password, request.NewPassword);
            if (!result.Succeeded)
            {
                return new Response { IsError = true, Message = "Current password is incorrect", StatusCode = 400 };
            }

            string emailSubject = "Password Changed";
            string userName = $"{user.FirstName} {user.LastName}";
            string emailBody = $@"
<html>
<head>
    <title>Password Changed</title>
</head>
<body>
    <p>Hello {userName},</p>
    <p>Your password has been successfully changed. If you did not initiate this change, please contact our support team immediately.</p>
    <p>Here are some tips to keep your account secure:</p>
    <ul>
        <li>Never share your password with anyone.</li>
    </ul>
    <p>Best regards,</p>
    <p>{tenantGetterService.Tenant.Name} Team</p>
</body>
</html>";

            await jobService.EnqueueAsync(
                new SendEmailRequestInput { Email = user.Email, Subject = emailSubject, HtmlMessage = emailBody },
                "SendEmailJob",
                CancellationToken.None);

            return new Response();
        }
    }

    internal sealed class Validator : AbstractValidator<ChangePasswordRequest>
    {
        public Validator()
        {
            RuleFor(p => p.Password)
                .NotEmpty().WithMessage("Current password is required");

            RuleFor(p => p.NewPassword)
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

            userGroup.MapPost("/change-password", async (
                    [FromBody] ChangePasswordRequest request,
                    [FromServices] Handler handler) =>
                {
                    Response res = await handler.ChangePasswordAsync(request);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .RequireAuthorization();
        }
    }
}
