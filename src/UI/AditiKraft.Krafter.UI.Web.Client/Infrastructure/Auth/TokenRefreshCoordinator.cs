namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public sealed class TokenRefreshCoordinator : IDisposable
{
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private long _sessionVersion;

    public long SessionVersion => Interlocked.Read(ref _sessionVersion);

    // An attempted sign-in supersedes automatic cleanup even while it waits for older transport.
    public void AdvanceSessionVersion() => Interlocked.Increment(ref _sessionVersion);

    // Complete automatic cleanup only if no newer sign-in intent arrived during its awaits.
    public bool TryAdvanceSessionVersion(long expectedVersion) =>
        Interlocked.CompareExchange(ref _sessionVersion, expectedVersion + 1, expectedVersion) == expectedVersion;

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public void Dispose() => _tokenLock.Dispose();
}
