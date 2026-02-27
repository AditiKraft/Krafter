# Background Jobs AI Instructions (TickerQ)

> **SCOPE**: Background job definitions and enqueue patterns.
> **PARENT**: See also: ../../Agents.md

## 1. Core Principles
- Job methods live in `src/AditiKraft.Krafter.Backend/Infrastructure/Jobs/JobService.cs`.
- Use `[TickerFunction(nameof(JobName))]` for each job method.
- Enqueue via `IJobService.EnqueueAsync(request, nameof(JobName), cancellationToken)`.

## 2. Decision Tree
- Adding a new job? Add a method to `Jobs` with `[TickerFunction]`.
- Need to run a job from a handler? Inject `IJobService` and call `EnqueueAsync`.

## 3. Code Templates

### Job Definition
```csharp
public class Jobs(IEmailService emailService)
{
    [TickerFunction(nameof(SendEmailJob))]
    public async Task SendEmailJob(TickerFunctionContext<SendEmailRequestInput> tickerContext,
        CancellationToken cancellationToken)
    {
        await emailService.SendEmailAsync(
            tickerContext.Request.Email,
            tickerContext.Request.Subject,
            tickerContext.Request.HtmlMessage);
    }
}
```

### Enqueue a Job
```csharp
await jobService.EnqueueAsync(
    new SendEmailRequestInput
    {
        Email = user.Email,
        Subject = "Welcome",
        HtmlMessage = "..."
    },
    nameof(Jobs.SendEmailJob),
    CancellationToken.None);
```

## 4. Checklist
1. Add the job method with `[TickerFunction(nameof(MyJob))]`.
2. Use `IJobService.EnqueueAsync(..., nameof(MyJob), ...)` from handlers.
3. Keep payloads as simple request DTOs.

## 5. Common Mistakes
- Hard-coding a job name string that does not match the method name.
- Calling `ITimeTickerManager` directly instead of `IJobService`.
- Enqueuing without a cancellation token.

## 6. Evolution Triggers
- Changes to `IJobService` or TickerQ usage.
- New job types added that need documentation.

---
Last Updated: 2026-01-25
Verified Against: Infrastructure/Jobs/JobService.cs
---

