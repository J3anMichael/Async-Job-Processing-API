using AsyncJobProcessingApi.Application.Interfaces;
using AsyncJobProcessingApi.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AsyncJobProcessingApi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IJobRepository _jobRepository;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        IJobRepository jobRepository, 
        IMessagePublisher messagePublisher,
        ILogger<JobsController> logger)
    {
        _jobRepository = jobRepository;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateJob([FromBody] CreateJobRequest request, CancellationToken cancellationToken)
    {
        var job = _jobRepository.CreateJob(request.Payload);
        
        _logger.LogInformation("Created job {JobId} with Pending status.", job.Id);

        try
        {
            await _messagePublisher.PublishJobAsync(job.Id, request.Payload, cancellationToken);
            return AcceptedAtAction(nameof(GetJob), new { jobId = job.Id }, new { JobId = job.Id, Status = job.Status.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish job {JobId}. Marking as failed.", job.Id);
            _jobRepository.UpdateJobStatus(job.Id, JobStatus.Failed, "Failed to enqueue job.");
            
            return StatusCode(500, new { Error = "Failed to enqueue job for processing." });
        }
    }

    [HttpGet("{jobId}")]
    public IActionResult GetJob(string jobId)
    {
        var job = _jobRepository.GetJob(jobId);

        if (job == null)
        {
            return NotFound(new { Error = $"Job {jobId} not found." });
        }

        return Ok(new
        {
            job.Id,
            Status = job.Status.ToString(),
            job.Result,
            job.CreatedAt
        });
    }
}

public class CreateJobRequest
{
    public string Payload { get; set; } = string.Empty;
}
