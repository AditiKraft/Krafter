namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public sealed class TokenRefreshCoordinator : IDisposable
{
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

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
