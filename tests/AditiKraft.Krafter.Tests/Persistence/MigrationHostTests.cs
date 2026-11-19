using AditiKraft.Krafter.Backend.Migrator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AditiKraft.Krafter.Tests.Persistence;

public sealed class MigrationHostTests
{
    [Fact]
    public async Task CompletedMigrationReturnsSuccessAndStopsHostAsync()
    {
        using IHost host = new HostBuilder().Build();
        IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        bool completed = false;

        int result = await MigrationHost.RunAsync(host, async cancellationToken =>
        {
            Assert.False(cancellationToken.IsCancellationRequested);
            await Task.Yield();
            completed = true;
        });

        Assert.True(completed);
        Assert.Equal(0, result);
        Assert.True(lifetime.ApplicationStopped.IsCancellationRequested);
    }

    [Fact]
    public async Task AsynchronousMigrationFailureReturnsFailureAndStopsHostAsync()
    {
        using IHost host = new HostBuilder().Build();
        int result = await MigrationHost.RunAsync(host, async _ =>
        {
            await Task.Yield();
            throw new InvalidOperationException("Controlled migration failure; no database used.");
        });

        Assert.Equal(1, result);
        Assert.True(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopped.IsCancellationRequested);
    }

    [Fact]
    public async Task ShutdownCancellationReturnsIncompleteStatusAsync()
    {
        using IHost host = new HostBuilder().Build();
        IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        int result = await MigrationHost.RunAsync(host, cancellationToken =>
        {
            lifetime.StopApplication();
            return Task.FromCanceled(cancellationToken);
        });

        Assert.Equal(2, result);
        Assert.True(lifetime.ApplicationStopped.IsCancellationRequested);
    }

    [Fact]
    public async Task UnrelatedCancellationIsFailureAsync()
    {
        using IHost host = new HostBuilder().Build();
        int result = await MigrationHost.RunAsync(host, _ => Task.FromCanceled(new CancellationToken(canceled: true)));

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ShutdownAfterCompletedWorkDoesNotChangeSuccessAsync()
    {
        using IHost host = new HostBuilder().Build();
        IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        int result = await MigrationHost.RunAsync(host, _ =>
        {
            lifetime.StopApplication();
            return Task.CompletedTask;
        });

        Assert.Equal(0, result);
    }
}
