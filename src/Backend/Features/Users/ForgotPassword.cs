using Backend.Api;
using Backend.Api.Authorization;
using Backend.Application.BackgroundJobs;
using Backend.Application.Notifications;
using Backend.Common;
using Backend.Common.Interfaces;
using Backend.Common.Models;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Backend.Features.Users;

public sealed class ForgotPassword
{
    public sealed class ForgotPasswordRequest
    {
        public string Email { get; set; } = default!;
    }

    internal sealed class Handler(
        UserManager<KrafterUser> userManager,
        ITenantGetterService tenantGetterService,
        IJobService jobService) : IScopedHandler
    {
        public async Task<Response> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            KrafterUser? user = await userManager.FindByEmailAsync(request.Email.Normalize());
            if (user is null)
            {
                return new Response { IsError = true, Message = "User Not Found", StatusCode = 404 };
            }

            string code = await userManager.GeneratePasswordResetTokenAsync(user);
            const string route = "account/reset-password";
            var endpointUri = new Uri(string.Concat($"{tenantGetterService.Tenant.TenantLink}/", route));
            string passwordResetUrl = QueryHelpers.AddQueryString(endpointUri.ToString(), "Token", code);

            string emailSubject = "Reset Password";
            string emailBody = $"Hello {user.FirstName} {user.LastName},<br/><br/>" +
                               "We received a request to reset your password. " +
                               "Please reset your password by clicking the link below:<br/><br/>" +
                               $"<a href='{passwordResetUrl}'>Reset Password</a><br/><br/>" +
                               "If you did not request a password reset, please ignore this email.<br/><br/>" +
                               $"Regards,<br/>{tenantGetterService.Tenant.Name} Team";

            await jobService.EnqueueAsync(
                new SendEmailRequestInput { Email = user.Email, Subject = emailSubject, HtmlMessage = emailBody },
                "SendEmailJob",
                CancellationToken.None);

            return new Response();
        }
    }

    internal sealed class Validator : AbstractValidator<ForgotPasswordRequest>
    {
        public Validator()
        {
            RuleFor(p => p.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format");
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Users)
                .AddFluentValidationFilter();

            userGroup.MapPost("/forgot-password", async (
                    [FromBody] ForgotPasswordRequest request,
                    [FromServices] Handler handler) =>
                {
                    Response res = await handler.ForgotPasswordAsync(request);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>();
        }
    }
}
