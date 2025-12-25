namespace Backend.Application.BackgroundJobs;

public interface IJobService
{
    public Task EnqueueAsync<T>(T requestInput, string methodName, CancellationToken cancellationToken);
}
