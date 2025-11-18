using RPA.API.Models;

namespace RPA.API.Services;

public interface IJobService
{
    Task<AutomationJob> CreateJobAsync(string name, string applicationPath, string? arguments, 
        List<AutomationStep> steps, JobPriority priority = JobPriority.Normal, string? webhookUrl = null);
    Task<AutomationJob?> GetJobAsync(Guid jobId);
    Task<List<AutomationJob>> GetJobsAsync(JobStatus? status = null, int skip = 0, int take = 50);
    Task<bool> UpdateJobStatusAsync(Guid jobId, JobStatus status, string? result = null, string? errorMessage = null);
    Task<bool> AssignJobAsync(Guid jobId, string agentId);
    Task<bool> AddScreenshotAsync(Guid jobId, string screenshotPath);
    Task<bool> CancelJobAsync(Guid jobId);
}

public class JobService : IJobService
{
    private readonly ILogger<JobService> _logger;
    private readonly IJobQueueService _queueService;
    private static readonly Dictionary<Guid, AutomationJob> _jobs = new();
    private readonly SemaphoreSlim _jobsSemaphore = new(1, 1);

    public JobService(ILogger<JobService> logger, IJobQueueService queueService)
    {
        _logger = logger;
        _queueService = queueService;
    }

    public async Task<AutomationJob> CreateJobAsync(string name, string applicationPath, string? arguments,
        List<AutomationStep> steps, JobPriority priority = JobPriority.Normal, string? webhookUrl = null)
    {
        await _jobsSemaphore.WaitAsync();
        try
        {
            var job = new AutomationJob
            {
                Name = name,
                ApplicationPath = applicationPath,
                Arguments = arguments,
                Steps = steps,
                Priority = priority,
                WebhookUrl = webhookUrl,
                Status = JobStatus.Pending
            };

            _jobs[job.Id] = job;
            
            // Queue the job
            await _queueService.EnqueueJobAsync(job.Id, priority);
            job.Status = JobStatus.Queued;
            
            _logger.LogInformation("Created job {JobId} '{JobName}' for application {ApplicationPath}",
                job.Id, job.Name, job.ApplicationPath);

            return job;
        }
        finally
        {
            _jobsSemaphore.Release();
        }
    }

    public async Task<AutomationJob?> GetJobAsync(Guid jobId)
    {
        await Task.CompletedTask;
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    public async Task<List<AutomationJob>> GetJobsAsync(JobStatus? status = null, int skip = 0, int take = 50)
    {
        await Task.CompletedTask;
        var query = _jobs.Values.AsQueryable();
        
        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);

        return query
            .OrderByDescending(j => j.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    public async Task<bool> UpdateJobStatusAsync(Guid jobId, JobStatus status, string? result = null, string? errorMessage = null)
    {
        await _jobsSemaphore.WaitAsync();
        try
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                return false;

            var oldStatus = job.Status;
            job.Status = status;

            if (status == JobStatus.Running && !job.StartedAt.HasValue)
                job.StartedAt = DateTime.UtcNow;
            
            if (status is JobStatus.Success or JobStatus.Failed or JobStatus.Cancelled or JobStatus.Timeout)
            {
                job.CompletedAt = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(result))
                    job.Result = result;
                if (!string.IsNullOrEmpty(errorMessage))
                    job.ErrorMessage = errorMessage;
            }

            _logger.LogInformation("Job {JobId} status changed from {OldStatus} to {NewStatus}",
                jobId, oldStatus, status);

            return true;
        }
        finally
        {
            _jobsSemaphore.Release();
        }
    }

    public async Task<bool> AssignJobAsync(Guid jobId, string agentId)
    {
        await _jobsSemaphore.WaitAsync();
        try
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                return false;

            job.AssignedAgentId = agentId;
            job.Status = JobStatus.Assigned;
            
            _logger.LogInformation("Assigned job {JobId} to agent {AgentId}", jobId, agentId);
            return true;
        }
        finally
        {
            _jobsSemaphore.Release();
        }
    }

    public async Task<bool> AddScreenshotAsync(Guid jobId, string screenshotPath)
    {
        await _jobsSemaphore.WaitAsync();
        try
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                return false;

            job.Screenshots.Add(screenshotPath);
            _logger.LogDebug("Added screenshot {ScreenshotPath} to job {JobId}", screenshotPath, jobId);
            return true;
        }
        finally
        {
            _jobsSemaphore.Release();
        }
    }

    public async Task<bool> CancelJobAsync(Guid jobId)
    {
        return await UpdateJobStatusAsync(jobId, JobStatus.Cancelled);
    }
}

public interface IJobQueueService
{
    Task EnqueueJobAsync(Guid jobId, JobPriority priority);
    Task<Guid?> DequeueJobAsync();
    Task<int> GetQueueLengthAsync();
}

public class JobQueueService : IJobQueueService
{
    private readonly ILogger<JobQueueService> _logger;
    private readonly PriorityQueue<Guid, int> _jobQueue = new();
    private readonly SemaphoreSlim _queueSemaphore = new(1, 1);

    public JobQueueService(ILogger<JobQueueService> logger)
    {
        _logger = logger;
    }

    public async Task EnqueueJobAsync(Guid jobId, JobPriority priority)
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            // Higher priority values get processed first (using negative for min-heap behavior)
            _jobQueue.Enqueue(jobId, -(int)priority);
            _logger.LogDebug("Enqueued job {JobId} with priority {Priority}", jobId, priority);
        }
        finally
        {
            _queueSemaphore.Release();
        }
    }

    public async Task<Guid?> DequeueJobAsync()
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            if (_jobQueue.Count == 0)
                return null;

            var jobId = _jobQueue.Dequeue();
            _logger.LogDebug("Dequeued job {JobId}", jobId);
            return jobId;
        }
        finally
        {
            _queueSemaphore.Release();
        }
    }

    public async Task<int> GetQueueLengthAsync()
    {
        await Task.CompletedTask;
        return _jobQueue.Count;
    }
}