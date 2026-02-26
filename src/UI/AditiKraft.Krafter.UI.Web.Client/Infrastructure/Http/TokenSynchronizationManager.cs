namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;

// Prevents multiple concurrent token refresh attempts - only one refresh executes, others wait and reuse the result
public static class TokenSynchronizationManager
{
    private static readonly SemaphoreSlim _synchronizationSemaphore = new(1, 1);
    private static volatile bool _isSynchronizing;
    private static DateTime _lastSyncTime = DateTime.MinValue;
    private static readonly TimeSpan RecentSyncThreshold = TimeSpan.FromSeconds(5);
    private const int MaxRetryAttempts = 2;

    public static bool IsSynchronizing => _isSynchronizing;

    public static async Task<bool> TryExecuteWithSynchronizationAsync<T>(
        Func<Task<T>> operation,
        Func<T, bool> isSuccessful,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        // Fast path: skip if recent sync succeeded
        if (HasRecentSync())
        {
            logger.LogInformation("Recent sync detected, skipping refresh operation");
            return true;
        }

        // Try non-blocking acquire first
        bool acquired = await _synchronizationSemaphore.WaitAsync(0, cancellationToken);

        if (!acquired)
        {
            // Another refresh in progress, wait for it
            logger.LogInformation("Token refresh already in progress, waiting for completion...");
            await _synchronizationSemaphore.WaitAsync(cancellationToken);
            _synchronizationSemaphore.Release();

            if (HasRecentSync())
            {
                logger.LogInformation("Waited for refresh, recent sync detected - assuming success");
                return true;
            }

            // Previous attempt failed, retry
            logger.LogInformation("Previous refresh attempt failed, retrying...");
            return await ExecuteWithRetryAsync(operation, isSuccessful, logger, cancellationToken);
        }

        return await ExecuteOperationAsync(operation, isSuccessful, logger, cancellationToken);
    }

    private static async Task<bool> ExecuteOperationAsync<T>(
        Func<Task<T>> operation,
        Func<T, bool> isSuccessful,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            _isSynchronizing = true;
            logger.LogInformation("Starting synchronized token refresh operation");

            T result = await operation();
            bool success = isSuccessful(result);

            if (success)
            {
                _lastSyncTime = DateTime.UtcNow;
                logger.LogInformation("Token refresh completed successfully");
            }
            else
            {
                logger.LogWarning("Token refresh operation failed");
            }

            return success;
        }
        finally
        {
            _isSynchronizing = false;
            _synchronizationSemaphore.Release();
        }
    }

    private static async Task<bool> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        Func<T, bool> isSuccessful,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            if (HasRecentSync())
            {
                logger.LogInformation("Recent sync detected during retry preparation, skipping");
                return true;
            }

            bool acquired = await _synchronizationSemaphore.WaitAsync(0, cancellationToken);

            if (!acquired)
            {
                // Another retry in progress, wait
                logger.LogInformation("Another retry in progress, waiting (attempt {Attempt}/{MaxAttempts})", attempt,
                    MaxRetryAttempts);
                await _synchronizationSemaphore.WaitAsync(cancellationToken);
                _synchronizationSemaphore.Release();

                if (HasRecentSync())
                {
                    logger.LogInformation("Retry succeeded by another caller");
                    return true;
                }

                continue;
            }

            logger.LogInformation("Executing retry attempt {Attempt}/{MaxAttempts}", attempt, MaxRetryAttempts);
            bool success = await ExecuteOperationAsync(operation, isSuccessful, logger, cancellationToken);

            if (success)
            {
                return true;
            }

            if (attempt < MaxRetryAttempts)
            {
                await Task.Delay(100 * attempt, cancellationToken);
            }
        }

        logger.LogWarning("All retry attempts exhausted");
        return false;
    }

    public static bool HasRecentSync(TimeSpan threshold) =>
        DateTime.UtcNow - _lastSyncTime < threshold;

    public static bool HasRecentSync() => HasRecentSync(RecentSyncThreshold);
}
