using System.Text.Json;
using Azure.Messaging.ServiceBus;
using AsyncJobProcessingApi.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;

namespace AsyncJobProcessingApi.Infrastructure.Messaging;

public class ServiceBusConsumer : IMessageConsumer, IAsyncDisposable
{
    private readonly ILogger<ServiceBusConsumer> _logger;
    private readonly ServiceBusClient _client;
    private readonly ServiceBusReceiver _receiver;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _maxConcurrentJobs;
    private readonly AsyncRetryPolicy _retryPolicy;

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

        var random = new Random();
        _retryPolicy = Policy
            .Handle<Exception>(ex => ex is not JsonException)
            .WaitAndRetryAsync(3, retryAttempt => 
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) 
                + TimeSpan.FromMilliseconds(random.Next(0, 1000)),
                onRetry: (exception, timeSpan, retryCount, context) => 
                {
                    var messageId = context.ContainsKey("MessageId") ? context["MessageId"] : "Unknown";
                    _logger.LogWarning(exception, "Retry {RetryAttempt} after {Delay}ms for MessageId {MessageId} due to {ExceptionMessage}", 
                        retryCount, timeSpan.TotalMilliseconds, messageId, exception.Message);
                });
    }

    public async Task StartConsumingAsync(CancellationToken cancellationToken)
    {
        var semaphore = new SemaphoreSlim(_maxConcurrentJobs);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await semaphore.WaitAsync(cancellationToken);
                
                int availableSlots = semaphore.CurrentCount + 1;
                
                var messages = await _receiver.ReceiveMessagesAsync(availableSlots, TimeSpan.FromSeconds(5), cancellationToken);
                
                semaphore.Release();

                if (messages == null || messages.Count == 0)
                {
                    continue;
                }

                foreach (var message in messages)
                {
                    await semaphore.WaitAsync(cancellationToken);

                    _ = ProcessMessageSafelyAsync(message, semaphore, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Worker connection to Service Bus was cancelled.");
        }
    }

    private async Task ProcessMessageSafelyAsync(ServiceBusReceivedMessage message, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var jobProcessor = scope.ServiceProvider.GetRequiredService<IJobProcessor>();

            string jobId;
            try
            {
                var messageBody = message.Body.ToString();
                using var document = JsonDocument.Parse(messageBody);
                jobId = document.RootElement.GetProperty("JobId").GetString() ?? throw new JsonException("JobId is null");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid message format captured. MessageId: {MessageId}", message.MessageId);
                await _receiver.DeadLetterMessageAsync(message, "InvalidFormat", "The message did not contain a valid JobId.", cancellationToken);
                return;
            }

            _logger.LogInformation("Invoking business UseCase for Job: {JobId}. MessageId: {MessageId}. Delivery Count: {Count}", 
                jobId, message.MessageId, message.DeliveryCount);

            var context = new Context { { "MessageId", message.MessageId } };
            
            await _retryPolicy.ExecuteAsync(async (ctx, ct) => 
            {
                await jobProcessor.ProcessJobAsync(jobId, ct);
            }, context, cancellationToken);

            await _receiver.CompleteMessageAsync(message, cancellationToken);
            _logger.LogInformation("Successfully processed and completed Job: {JobId}. MessageId: {MessageId}.", jobId, message.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message {MessageId} after retries. Abandoning to Service Bus.", message.MessageId);
            await _receiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
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
