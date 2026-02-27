using AditiKraft.Krafter.Backend.Api;
using AditiKraft.Krafter.Backend.Jobs;
using AditiKraft.Krafter.Backend.Notifications;
using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Features.Users._Shared;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace AditiKraft.Krafter.Backend.Features.Users;

public sealed class ForgotPassword
{
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

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                await jobService.EnqueueAsync(
                    new SendEmailRequestInput { Email = user.Email, Subject = emailSubject, HtmlMessage = emailBody },
                    "SendEmailJob",
                    CancellationToken.None);
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

            userGroup.MapPost($"/{RouteSegment.ForgotPassword}", async (
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

