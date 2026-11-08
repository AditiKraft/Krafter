# Background Jobs AI Instructions (TickerQ)

> **SCOPE**: Background job definitions and enqueue patterns.
> **PARENT**: See also: ../../AGENTS.md

## 1. Core Principles
- Job methods live in `src/AditiKraft.Krafter.Backend/Infrastructure/Jobs/Jobs.cs`. `JobService.cs` contains the enqueue implementation.
- Use `[TickerFunction(nameof(JobName))]` for each job method.
- Enqueue via `IJobService.EnqueueAsync(request, nameof(Jobs.JobName), cancellationToken)` from callers.
- `JobService` checks TickerQ's result and propagates the original failure/cancellation. A completed call requires a successful enqueue result; callers must propagate or record failures internally. Anonymous password recovery logs only a fixed sanitized failure and keeps its generic public response to avoid revealing account existence; caller cancellation still propagates.
- Do not blindly retry failed enqueue results: TickerQ can report a notification failure after persisting the job. Domain state and the operational job store do not share an outbox transaction.
- Each TickerQ attempt owns a DI scope. Keep `IEmailService` and `SmtpClient` scoped so concurrent jobs have independent clients, and pass the job cancellation token through `SendMailAsync`.

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
            tickerContext.Request.HtmlMessage,
            cancellationToken);
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
2. Use `IJobService.EnqueueAsync(..., nameof(Jobs.MyJob), ...)` from handlers.
3. Keep payloads as simple request DTOs.

## 5. Common Mistakes
- Hard-coding a job name string that does not match the method name.
- Calling `ITimeTickerManager` directly instead of `IJobService`.
- Enqueuing without a cancellation token.
- Registering a singleton SMTP client: `SmtpClient` does not support overlapping sends.

## 6. Evolution Triggers
- Changes to `IJobService` or TickerQ usage.
- New job types added that need documentation.

---
Last Updated: 2026-11-08
Verified Against: src/AditiKraft.Krafter.Backend/Infrastructure/Jobs/Jobs.cs, src/AditiKraft.Krafter.Backend/Infrastructure/Jobs/JobService.cs, src/AditiKraft.Krafter.Backend/Infrastructure/Notifications/NotificationsConfiguration.cs, src/AditiKraft.Krafter.Backend/Infrastructure/Notifications/EmailService.cs, tests/AditiKraft.Krafter.Tests/Jobs/JobServiceTests.cs, tests/AditiKraft.Krafter.Tests/Notifications/EmailServiceTests.cs, src/AditiKraft.Krafter.Backend/Features/Users/CreateUser.cs, src/AditiKraft.Krafter.Backend/Features/Users/ChangePassword.cs, src/AditiKraft.Krafter.Backend/Features/Users/ForgotPassword.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/Common/DataSeedService.cs
---

