using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using TickerQ.Utilities;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models;
using TickerQ.Utilities.Models.Ticker;

namespace AditiKraft.Krafter.Backend.Infrastructure.BackgroundJobs;

public class Jobs(IEmailService emailService)
{
    [TickerFunction(nameof(SendEmailJobAsync))]
    public async Task SendEmailJobAsync(TickerFunctionContext<SendEmailRequestInput> tickerContext,
        CancellationToken cancellationToken)
    {
        await emailService.SendEmailAsync(
            tickerContext.Request.Email,
            tickerContext.Request.Subject,
            tickerContext.Request.HtmlMessage, cancellationToken
        );
    }
}

public class JobService(ITimeTickerManager<TimeTicker> timeTickerManager)
    : IJobService
{
    public async Task EnqueueAsync<T>(T requestInput, string methodName, CancellationToken cancellationToken)
    {
        TickerResult<TimeTicker>? res = await timeTickerManager.AddAsync(new TimeTicker
        {
            Request = TickerHelper.CreateTickerRequest(requestInput),
            ExecutionTime = DateTime.Now.AddSeconds(1),
            Function = methodName,
            Description = $"Short Description",
            Retries = 3,
            RetryIntervals = [20, 60, 100] // set in seconds
        }, cancellationToken);
    }
}


