using AsyncJobProcessingApi.Application.Interfaces;
using Microsoft.Extensions.Hosting;

namespace AsyncJobProcessingApi.Workers;

public class ServiceBusWorker : BackgroundService
{
    private readonly IMessageConsumer _consumer;

    public ServiceBusWorker(IMessageConsumer consumer)
    {
        _consumer = consumer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _consumer.StartConsumingAsync(stoppingToken);
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _consumer.StopConsumingAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
