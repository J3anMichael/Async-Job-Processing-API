using System.Text.Json;
using Azure.Messaging.ServiceBus;
using AsyncJobProcessingApi.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AsyncJobProcessingApi.Infrastructure;

public class ServiceBusPublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;
    private readonly ILogger<ServiceBusPublisher> _logger;

    public ServiceBusPublisher(IConfiguration configuration, ILogger<ServiceBusPublisher> logger)
    {
        _logger = logger;
        
        var connectionString = configuration["ServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("A configuração 'ConnectionString' é obrigatória no appsettings.json!");
        var queueName = configuration["ServiceBus:QueueName"]
            ?? throw new InvalidOperationException("A configuração 'QueueName' é obrigatória no appsettings.json!");

        _client = new ServiceBusClient(connectionString);
        _sender = _client.CreateSender(queueName);
    }

    public async Task PublishJobAsync(string jobId, string payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var messageContent = new { JobId = jobId, Payload = payload };
            var messageBody = JsonSerializer.Serialize(messageContent);
            
            var serviceBusMessage = new ServiceBusMessage(messageBody)
            {
                MessageId = jobId,
                ApplicationProperties =
                {
                    { "JobType", "StandardProcessing" }
                }
            };

            await _sender.SendMessageAsync(serviceBusMessage, cancellationToken);
            _logger.LogInformation("Successfully published JobId {JobId} to Service Bus.", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish JobId {JobId} to Service Bus.", jobId);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
