using System.Net.Mail;
using System.Security.Claims;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.Backend.Features.Auth;

public sealed class RequestEnrollmentEmail
{
    internal sealed class Handler(TwoFactorService twoFactor, IEmailService email) : IScopedHandler
    {
        public async Task<Response<TwoFactorChallengeResponse>> SendAsync(string userId, ClaimsPrincipal principal,
            CancellationToken cancellationToken)
        {
            var result = await twoFactor.PrepareEnrollmentEmailAsync(userId, principal, cancellationToken);
            if (result.IsError || result.Data is null)
                return Response<TwoFactorChallengeResponse>.CustomError(result.Message ?? "Email verification unavailable.", result.StatusCode);

            // Deliver after the account transaction commits. Keep the plaintext code out of the job store.
            var delivery = result.Data;
            try
            {
                await email.SendEmailAsync(delivery.Address, "Verify authenticator setup",
                    $"Your authenticator setup code is <strong>{delivery.Code}</strong>. It expires at {delivery.Challenge.ExpiresAt.UtcDateTime.ToString("u", System.Globalization.CultureInfo.InvariantCulture)} (UTC). " +
                    "Enter it in the account settings tab where you requested it. If you did not request it, ignore this email.", cancellationToken);
            }
            catch (Exception exception) when (exception is SmtpException or FormatException or InvalidOperationException)
            {
                return Response<TwoFactorChallengeResponse>.CustomError("Unable to send the verification email. Try again in one minute.", 503);
            }

            return Response<TwoFactorChallengeResponse>.Success(delivery.Challenge);
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapTwoFactorGroup().MapPost("/enrollment-email", async (
                HttpContext context, Handler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.SendAsync(TwoFactorEndpoints.UserId(context), context.User, cancellationToken);
                return Results.Json(result, statusCode: result.StatusCode);
            }).RequireAuthorization().Produces<Response<TwoFactorChallengeResponse>>();
        }
    }
}
