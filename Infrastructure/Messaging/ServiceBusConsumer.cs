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
            ?? throw new InvalidOperationException("A configuração 'ConnectionString' é obrigatória no appsettings.json!");
        var queueName = configuration["ServiceBus:QueueName"]
            ?? throw new InvalidOperationException("A configuração 'QueueName' é obrigatória no appsettings.json!");

        _maxConcurrentJobs = configuration.GetValue<int?>("ServiceBus:MaxConcurrentJobs")
            ?? throw new InvalidOperationException("A configuração 'MaxConcurrentJobs' é obrigatória no appsettings.json!");

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
                            _logger.LogError(ex, "Ocorreu uma falha ao processar a mensagem {MessageId}", message.MessageId);
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
            _logger.LogInformation("Conexão do Worker com o Service Bus foi cancelada.");
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
            _logger.LogWarning("Mensagem com formato inválido captada. MessageId: {MessageId}", message.MessageId);
            await _receiver.DeadLetterMessageAsync(message, "InvalidFormat", "The message did not contain a valid JobId.");
            return;
        }

        _logger.LogInformation("Deixando a camada de Infra e mandando para o UseCase de negócio para a Job: {JobId}. Delivery Count: {Count}", jobId, message.DeliveryCount);

        // Chama o processador de regra de negócio, separadamente!
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
