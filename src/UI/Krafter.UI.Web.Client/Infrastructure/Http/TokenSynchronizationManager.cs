namespace Krafter.UI.Web.Client.Infrastructure.Http;

public static class TokenSynchronizationManager
{
    private static readonly SemaphoreSlim _synchronizationSemaphore = new(1, 1);
    private static volatile bool _isSynchronizing = false;
    private static DateTime _lastSyncTime = DateTime.MinValue;

    public static bool IsSynchronizing => _isSynchronizing;

    public static async Task<bool> TryExecuteWithSynchronizationAsync<T>(
        Func<Task<T>> operation,
        Func<T, bool> isSuccessful,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        // If already synchronizing, wait for it to complete
        if (_isSynchronizing)
        {
            logger.LogInformation("Synchronization in progress, waiting...");
            await _synchronizationSemaphore.WaitAsync(cancellationToken);
            _synchronizationSemaphore.Release();
            return true; // Assume it succeeded since we waited
        }

        // Execute with synchronization lock
        await _synchronizationSemaphore.WaitAsync(cancellationToken);
        try
        {
            _isSynchronizing = true;
            logger.LogInformation("Starting synchronized operation");

            T result = await operation();
            bool success = isSuccessful(result);

            if (success)
            {
                _lastSyncTime = DateTime.UtcNow;
            }

            logger.LogInformation("Synchronized operation completed: {Success}", success);
            return success;
        }
        finally
        {
            _isSynchronizing = false;
            _synchronizationSemaphore.Release();
        }
    }

    public static async Task WaitForSynchronizationAsync(CancellationToken cancellationToken = default)
    {
        if (!_isSynchronizing)
        {
            return;
        }

        await _synchronizationSemaphore.WaitAsync(cancellationToken);
        _synchronizationSemaphore.Release();
    }

    public static bool HasRecentSync(TimeSpan threshold) =>
        DateTime.UtcNow - _lastSyncTime < threshold;
}
