namespace AditiKraft.Krafter.Backend.Jobs;

public interface IJobService
{
    public Task EnqueueAsync<T>(T requestInput, string methodName, CancellationToken cancellationToken);
}

