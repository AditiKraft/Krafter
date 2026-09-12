using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Models;

namespace AditiKraft.Krafter.Backend.Infrastructure.Jobs;

public class Jobs(IEmailService emailService)
{
    [TickerFunction(nameof(SendEmailJob))]
    public async Task SendEmailJob(TickerFunctionContext<SendEmailRequestInput> tickerContext,
        CancellationToken cancellationToken)
    {
        await emailService.SendEmailAsync(
            tickerContext.Request.Email,
            tickerContext.Request.Subject,
            tickerContext.Request.HtmlMessage, cancellationToken
        );
    }
}
