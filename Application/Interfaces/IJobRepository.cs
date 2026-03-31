using AsyncJobProcessingApi.Domain.Entities;

namespace AsyncJobProcessingApi.Application.Interfaces;

public interface IJobRepository
{
    Job CreateJob(string payload);
    Job? GetJob(string jobId);
    void UpdateJobStatus(string jobId, JobStatus status, string? result = null);
}
