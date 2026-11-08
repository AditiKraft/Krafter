using AditiKraft.Krafter.Backend.Migrator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// A process-level check of the production migration runner; no database or web host.
using IHost host = new HostBuilder().Build();
IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
string mode = args.Single();
return await MigrationHost.RunAsync(host, async cancellationToken =>
{
    await Task.Yield();
    switch (mode)
    {
        case "success":
            return;
        case "failure":
            throw new InvalidOperationException("Controlled migration failure; no database used.");
        case "shutdown-cancellation":
            lifetime.StopApplication();
            cancellationToken.ThrowIfCancellationRequested();
            return;
        case "unrelated-cancellation":
            throw new OperationCanceledException(new CancellationToken(canceled: true));
        case "success-then-shutdown":
            lifetime.StopApplication();
            return;
        default:
            throw new ArgumentException("Expected success, failure, shutdown-cancellation, unrelated-cancellation, or success-then-shutdown.");
    }
});
