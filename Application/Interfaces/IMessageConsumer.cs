namespace AsyncJobProcessingApi.Application.Interfaces;

public interface IMessageConsumer
{
    Task StartConsumingAsync(CancellationToken cancellationToken);
    Task StopConsumingAsync(CancellationToken cancellationToken);
}
