namespace AsyncJobProcessingApi.Application.Interfaces;

public interface IMessagePublisher
{
    Task PublishJobAsync(string jobId, string payload, CancellationToken cancellationToken = default);
}
