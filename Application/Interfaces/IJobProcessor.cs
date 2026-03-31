namespace AsyncJobProcessingApi.Application.Interfaces;

public interface IJobProcessor
{
    Task ProcessJobAsync(string jobId, CancellationToken cancellationToken = default);
}
