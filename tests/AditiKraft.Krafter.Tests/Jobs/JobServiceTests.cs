using System.Reflection;
using System.Runtime.CompilerServices;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models;
using TickerQ.Utilities.Models.Ticker;
using BackgroundJobs = AditiKraft.Krafter.Backend.Infrastructure.Jobs.Jobs;

namespace AditiKraft.Krafter.Tests.Jobs;

public sealed class JobServiceTests
{
    [Fact]
    public async Task FailedResultPropagatesTheOriginalExceptionAndStackWithoutRetrying()
    {
        Exception failure = CapturePersistenceFailure();
        (JobService service, TimeTickerManagerProxy proxy) = Create((_, _) =>
            new TickerResult<TimeTicker>(failure));

        Exception observed = await Assert.ThrowsAsync<InvalidOperationException>(() => EnqueueAsync(service));

        Assert.Same(failure, observed);
        Assert.Contains(nameof(CapturePersistenceFailure), observed.StackTrace);
        Assert.Equal(1, proxy.AddCalls);
    }

    [Fact]
    public async Task CancellationReturnedByTheManagerRemainsCancellationForTheCaller()
    {
        using var cancellation = new CancellationTokenSource();
        var failure = new OperationCanceledException("Operational-store insert canceled", cancellation.Token);
        (JobService service, TimeTickerManagerProxy proxy) = Create((_, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            return new TickerResult<TimeTicker>(failure);
        });

        OperationCanceledException observed = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            EnqueueAsync(service, cancellation.Token));

        Assert.Same(failure, observed);
        Assert.Equal(cancellation.Token, observed.CancellationToken);
        Assert.Equal(1, proxy.AddCalls);
    }

    [Fact]
    public async Task AlreadyCanceledCallerDoesNotAttemptInsertion()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        (JobService service, TimeTickerManagerProxy proxy) = Create((ticker, _) =>
            new TickerResult<TimeTicker>(ticker));

        OperationCanceledException observed = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            EnqueueAsync(service, cancellation.Token));

        Assert.Equal(cancellation.Token, observed.CancellationToken);
        Assert.Equal(0, proxy.AddCalls);
    }

    [Fact]
    public async Task SuccessfulResultEnqueuesOnceAndPassesTheJobAndCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        (JobService service, TimeTickerManagerProxy proxy) = Create((ticker, _) =>
            new TickerResult<TimeTicker>(ticker));

        await EnqueueAsync(service, cancellation.Token);

        Assert.Equal(1, proxy.AddCalls);
        Assert.Equal(cancellation.Token, proxy.ObservedCancellation);
        Assert.NotNull(proxy.ObservedTicker);
        Assert.Equal(nameof(BackgroundJobs.SendEmailJob), proxy.ObservedTicker.Function);
        Assert.NotEmpty(proxy.ObservedTicker.Request);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingFailureDetailsStillFailExplicitly(bool nullResult)
    {
        (JobService service, TimeTickerManagerProxy proxy) = Create((_, _) => nullResult
            ? null!
            : new TickerResult<TimeTicker>((Exception)null!));

        InvalidOperationException observed = await Assert.ThrowsAsync<InvalidOperationException>(() => EnqueueAsync(service));

        Assert.Contains(nameof(BackgroundJobs.SendEmailJob), observed.Message);
        Assert.Equal(1, proxy.AddCalls);
    }

    private static Task EnqueueAsync(JobService service, CancellationToken cancellationToken = default) =>
        service.EnqueueAsync(new SendEmailRequestInput
        {
            Email = "recipient@example.test", Subject = "Queued test", HtmlMessage = "<p>Test</p>"
        }, nameof(BackgroundJobs.SendEmailJob), cancellationToken);

    private static (JobService Service, TimeTickerManagerProxy Proxy) Create(
        Func<TimeTicker, CancellationToken, TickerResult<TimeTicker>> response)
    {
        ITimeTickerManager<TimeTicker> manager =
            DispatchProxy.Create<ITimeTickerManager<TimeTicker>, TimeTickerManagerProxy>();
        var proxy = (TimeTickerManagerProxy)manager;
        proxy.Response = response;
        return (new JobService(manager), proxy);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CapturePersistenceFailure()
    {
        try { throw new InvalidOperationException("Operational-store insert failed"); }
        catch (InvalidOperationException exception) { return exception; }
    }

    // Use the actual pinned TickerQ result type without inventing unrelated manager methods.
    public class TimeTickerManagerProxy : DispatchProxy
    {
        public Func<TimeTicker, CancellationToken, TickerResult<TimeTicker>> Response { get; set; } = null!;
        public int AddCalls { get; private set; }
        public TimeTicker? ObservedTicker { get; private set; }
        public CancellationToken ObservedCancellation { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != "AddAsync" || args is not { Length: 2 } || args[0] is not TimeTicker ticker)
                throw new NotSupportedException($"Unexpected manager method {targetMethod?.Name}.");

            AddCalls++;
            ObservedTicker = ticker;
            ObservedCancellation = (CancellationToken)args[1]!;
            return Task.FromResult(Response(ticker, ObservedCancellation));
        }
    }
}
