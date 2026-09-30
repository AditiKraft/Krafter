using System.Collections.Concurrent;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

/// <summary>
/// Serialises refresh-token rotation per user so that concurrent refresh requests for the same
/// user are processed one at a time. Combined with the grace window on
/// <see cref="UserRefreshToken.PreviousRefreshToken"/>, this prevents the rotation race where two
/// parallel callers present the same (expired) refresh token and all but the last writer are left
/// holding a dead token.
/// <para>
/// NOTE: this lock is process-local. In a scaled-out (multi-instance) deployment it only serialises
/// within a single instance; the persisted grace window remains the cross-instance safety net, but
/// a distributed lock or DB-level optimistic concurrency would be required for full correctness
/// under simultaneous cross-instance refreshes.
/// </para>
/// </summary>
internal static class RefreshTokenLock
{
    private sealed class RefCountedSemaphore
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount;
    }

    private static readonly ConcurrentDictionary<string, RefCountedSemaphore> Locks = new();
    private static readonly object CleanupGate = new();

    /// <summary>
    /// Runs <paramref name="action"/> while holding the per-user lock. Entries are reference-counted
    /// so the semaphore is removed only once no request is holding or waiting on it, which avoids the
    /// removal race where a waiter could otherwise acquire a semaphore that was concurrently evicted.
    /// </summary>
    public static async Task<T> RunAsync<T>(string userId, Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        RefCountedSemaphore entry = Acquire(userId);
        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
        }
        catch
        {
            // The wait was cancelled/faulted before the semaphore was taken, so undo the refcount
            // acquired above (the finally below never runs because we never entered the try).
            Release(userId, entry);
            throw;
        }

        try
        {
            return await action();
        }
        finally
        {
            entry.Semaphore.Release();
            Release(userId, entry);
        }
    }

    private static RefCountedSemaphore Acquire(string userId)
    {
        lock (CleanupGate)
        {
            RefCountedSemaphore entry = Locks.GetOrAdd(userId, _ => new RefCountedSemaphore());
            entry.RefCount++;
            return entry;
        }
    }

    private static void Release(string userId, RefCountedSemaphore entry)
    {
        lock (CleanupGate)
        {
            if (--entry.RefCount == 0)
            {
                Locks.TryRemove(userId, out _);
                entry.Semaphore.Dispose();
            }
        }
    }
}
