using System.Text.Encodings.Web;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using AditiKraft.Krafter.Backend.Web;
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
        UserManager<ApplicationUser> userManager,
        ITenantGetterService tenantGetterService,
        IJobService jobService,
        ILogger<ForgotPassword> logger) : IScopedHandler
    {
        public async Task<Response> ForgotPasswordAsync(ForgotPasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplicationUser? user = await userManager.FindByEmailAsync(request.Email.Normalize());
            cancellationToken.ThrowIfCancellationRequested();
            const string message = "If the account is eligible, you will receive a password reset link.";
            if (user is not { IsActive: true, IsDeleted: false })
            {
                return Response.Success(message);
            }

            string code = await userManager.GeneratePasswordResetTokenAsync(user);
            cancellationToken.ThrowIfCancellationRequested();
            const string route = "account/reset-password";
            var endpointUri = new Uri(string.Concat($"{tenantGetterService.Tenant.TenantLink}/", route));
            string passwordResetUrl = QueryHelpers.AddQueryString(endpointUri.ToString(), "Token", code);

            string emailSubject = "Reset Password";
            HtmlEncoder html = HtmlEncoder.Default;
            string emailBody = $"Hello {html.Encode(user.FirstName ?? string.Empty)} {html.Encode(user.LastName ?? string.Empty)},<br/><br/>" +
                               "We received a request to reset your password. " +
                               "Please reset your password by clicking the link below:<br/><br/>" +
                               $"<a href='{html.Encode(passwordResetUrl)}'>Reset Password</a><br/><br/>" +
                               "If you did not request a password reset, please ignore this email.<br/><br/>" +
                               $"Regards,<br/>{html.Encode(tenantGetterService.Tenant.Name ?? string.Empty)} Team";

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                try
                {
                    await jobService.EnqueueAsync(
                        new SendEmailRequestInput { Email = user.Email, Subject = emailSubject, HtmlMessage = emailBody },
                        nameof(Jobs.SendEmailJob),
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // Queue availability must not reveal whether this anonymous request found an account.
                    // Exception messages/payloads can contain email addresses and reset credentials.
                    logger.LogWarning("Password reset email could not be queued. Failure type: {FailureType}.",
                        exception.GetType().Name);
                }
            }

            return Response.Success(message);
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(ApiRoutes.Users)
                .AddFluentValidationFilter();

            userGroup.MapPost($"/{RouteSegment.ForgotPassword}", async (
                    [FromBody] ForgotPasswordRequest request,
                    [FromServices] Handler handler,
                    CancellationToken cancellationToken) =>
                {
                    Response res = await handler.ForgotPasswordAsync(request, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .RequireRateLimiting("authentication");
        }
    }
}
