using System.Runtime.ExceptionServices;
using TickerQ.Utilities;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models;
using TickerQ.Utilities.Models.Ticker;

namespace AditiKraft.Krafter.Backend.Infrastructure.Jobs;

public class JobService(ITimeTickerManager<TimeTicker> timeTickerManager)
    : IJobService
{
    public async Task EnqueueAsync<T>(T requestInput, string methodName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TickerResult<TimeTicker>? result = await timeTickerManager.AddAsync(new TimeTicker
        {
            Request = TickerHelper.CreateTickerRequest(requestInput),
            ExecutionTime = DateTime.UtcNow.AddSeconds(1),
            Function = methodName,
            Description = $"Short Description",
            Retries = 3,
            RetryIntervals = [20, 60, 100] // set in seconds
        }, cancellationToken);

        if (result?.IsSucceded == true) return;

        // TickerQ returns persistence failures (including cancellation) as a result.
        // Do not retry here: failure can also occur after insertion during notification.
        if (result?.Exception is { } exception)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException($"Failed to enqueue job '{methodName}'.");
    }
}
