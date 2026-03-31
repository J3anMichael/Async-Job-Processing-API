using AsyncJobProcessingApi.Application.Interfaces;
using AsyncJobProcessingApi.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AsyncJobProcessingApi.Application.UseCases;

public class JobProcessor : IJobProcessor
{
    private readonly IJobRepository _jobRepository;
    private readonly ILogger<JobProcessor> _logger;

    public JobProcessor(IJobRepository jobRepository, ILogger<JobProcessor> logger)
    {
        _jobRepository = jobRepository;
        _logger = logger;
    }

    public async Task ProcessJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting business logic processing for job {JobId}", jobId);

        // Mudar status para 'Processing'
        _jobRepository.UpdateJobStatus(jobId, JobStatus.Processing);

        // Simular trabalho pesado ou chamada de API externa
        var simulationDelay = new Random().Next(2000, 5000);
        await Task.Delay(simulationDelay, cancellationToken);
        
        var result = $"Processed successfully in {simulationDelay}ms";

        // Mudar status para 'Completed' no banco simulado
        _jobRepository.UpdateJobStatus(jobId, JobStatus.Completed, result);
        
        _logger.LogInformation("Completed business logic for job {JobId}. Result: {Result}", jobId, result);
    }
}
