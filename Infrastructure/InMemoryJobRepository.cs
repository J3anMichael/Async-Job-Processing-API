using System.Collections.Concurrent;
using AsyncJobProcessingApi.Application.Interfaces;
using AsyncJobProcessingApi.Domain.Entities;

namespace AsyncJobProcessingApi.Infrastructure;

public class InMemoryJobRepository : IJobRepository
{
    private readonly ConcurrentDictionary<string, Job> _jobs = new();

    public Job CreateJob(string payload)
    {
        var job = new Job
        {
            Id = Guid.NewGuid().ToString(),
            Payload = payload,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _jobs.TryAdd(job.Id, job);
        return job;
    }

    public Job? GetJob(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            return job;
        }

        return null;
    }

    public void UpdateJobStatus(string jobId, JobStatus status, string? result = null)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = status;
            if (result != null)
            {
                job.Result = result;
            }
        }
    }
}
