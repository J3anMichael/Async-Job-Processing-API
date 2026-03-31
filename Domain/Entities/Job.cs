namespace AsyncJobProcessingApi.Domain.Entities;

public class Job
{
    public string Id { get; set; } = string.Empty;
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public string? Payload { get; set; }
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
