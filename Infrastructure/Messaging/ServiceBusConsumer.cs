using System.Text.Json;
using Azure.Messaging.ServiceBus;
using AsyncJobProcessingApi.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncJobProcessingApi.Infrastructure.Messaging;

public class ServiceBusConsumer : IMessageConsumer, IAsyncDisposable
{
    private readonly ILogger<ServiceBusConsumer> _logger;
    private readonly ServiceBusClient _client;
    private readonly ServiceBusReceiver _receiver;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _maxConcurrentJobs;

    public ServiceBusConsumer(
        IConfiguration configuration, 
        ILogger<ServiceBusConsumer> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        var connectionString = configuration["ServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("Configuration 'ConnectionString' is required in appsettings.json!");
        var queueName = configuration["ServiceBus:QueueName"]
            ?? throw new InvalidOperationException("Configuration 'QueueName' is required in appsettings.json!");

        _maxConcurrentJobs = configuration.GetValue<int?>("ServiceBus:MaxConcurrentJobs")
            ?? throw new InvalidOperationException("Configuration 'MaxConcurrentJobs' is required in appsettings.json!");

        _client = new ServiceBusClient(connectionString);
        
        var receiverOptions = new ServiceBusReceiverOptions
        {
            PrefetchCount = _maxConcurrentJobs,
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        };
        
        _receiver = _client.CreateReceiver(queueName, receiverOptions);
    }

    public async Task StartConsumingAsync(CancellationToken cancellationToken)
    {
        var semaphore = new SemaphoreSlim(_maxConcurrentJobs);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await _receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5), cancellationToken);
                
                if (message != null)
                {
                    await semaphore.WaitAsync(cancellationToken);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessMessageAsync(message, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process message {MessageId}", message.MessageId);
                            await _receiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Worker connection to Service Bus was cancelled.");
        }
    }

    private async Task ProcessMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var jobProcessor = scope.ServiceProvider.GetRequiredService<IJobProcessor>();
        
        var messageBody = message.Body.ToString();
        using var document = JsonDocument.Parse(messageBody);
        var jobId = document.RootElement.GetProperty("JobId").GetString();

        if (string.IsNullOrEmpty(jobId))
        {
            _logger.LogWarning("Invalid message format captured. MessageId: {MessageId}", message.MessageId);
            await _receiver.DeadLetterMessageAsync(message, "InvalidFormat", "The message did not contain a valid JobId.");
            return;
        }

        _logger.LogInformation("Invoking business UseCase for Job: {JobId}. Delivery Count: {Count}", jobId, message.DeliveryCount);

        await jobProcessor.ProcessJobAsync(jobId, cancellationToken);

        await _receiver.CompleteMessageAsync(message, cancellationToken);
    }

    public async Task StopConsumingAsync(CancellationToken cancellationToken)
    {
        await _receiver.CloseAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _receiver.DisposeAsync();
        await _client.DisposeAsync();
    }
}
