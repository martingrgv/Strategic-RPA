using RPA.Orchestrator.Models;

namespace RPA.Orchestrator.Services;

public interface IJobScheduler
{
    Task<Guid> ScheduleJobAsync(AutomationJob job);
    Task<bool> CancelJobAsync(Guid jobId);
    Task<AutomationJob?> GetJobAsync(Guid jobId);
    Task<List<AutomationJob>> GetQueuedJobsAsync();
    Task<List<AutomationJob>> GetJobsByStatusAsync(JobStatus status);
    Task<bool> UpdateJobStatusAsync(Guid jobId, JobStatus status, string? result = null, string? error = null);
    Task<List<AutomationJob>> GetTimedOutJobsAsync(TimeSpan timeout);
    Task<int> GetJobCountByPriorityAsync(int priority);
    Task ProcessJobQueueAsync();
}

public class JobScheduler : IJobScheduler
{
    private readonly ILogger<JobScheduler> _logger;
    private readonly IAgentManager _agentManager;
    private static readonly Dictionary<Guid, AutomationJob> _jobs = new();
    private static readonly PriorityQueue<Guid, int> _jobQueue = new();
    private readonly SemaphoreSlim _jobsSemaphore = new(1, 1);
    private readonly SemaphoreSlim _queueSemaphore = new(1, 1);

    public JobScheduler(ILogger<JobScheduler> logger, IAgentManager agentManager)
    {
        _logger = logger;
        _agentManager = agentManager;
    }

    public async Task<bool> EnqueueJobAsync(AutomationJob job)
    {
        await _jobsSemaphore.WaitAsync();
        try
        {
            _jobs[job.Id] = job;
            job.Status = JobStatus.Queued;
            job.QueuedAt = DateTime.UtcNow;

            await _queueSemaphore.WaitAsync();
            try
            {
                // Higher priority values get processed first (using negative for min-heap behavior)
                _jobQueue.Enqueue(job.Id, -(int)job.Priority);
            }
            finally
            {
                _queueSemaphore.Release();
            }

            _logger.LogInformation("Job {JobId} '{JobName}' queued with priority {Priority}", 
                job.Id, job.Name, job.Priority);

            return true;
        }
        finally
        {
            _jobsSemaphore.Release();
        }
    }

    public async Task<AutomationJob?> DequeueJobAsync()
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            if (_jobQueue.Count == 0)
                return null;

            var jobId = _jobQueue.Dequeue();
            
            await _jobsSemaphore.WaitAsync();
            try
            {
                if (_jobs.TryGetValue(jobId, out var job))
                {
                    job.Status = JobStatus.Assigned;
                    job.AssignedAt = DateTime.UtcNow;
                    
                    _logger.LogDebug("Dequeued job {JobId} '{JobName}'", job.Id, job.Name);
                    return job;
                }
            }
            finally
            {
                _jobsSemaphore.Release();
            }
        }
        finally
        {
            _queueSemaphore.Release();
        }

        return null;
    }

    public async Task<List<AutomationJob>> GetQueuedJobsAsync()
    {
        await Task.CompletedTask;
        return _jobs.Values
            .Where(j => j.Status == JobStatus.Queued)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.QueuedAt)
            .ToList();
    }

    public async Task<AutomationJob?> GetJobAsync(Guid jobId)
    {
        await Task.CompletedTask;
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    public async Task<bool> UpdateJobStatusAsync(Guid jobId, JobStatus status, string? result = null, string? error = null)
    {
        await _jobsSemaphore.WaitAsync();
        try
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                return false;

            var oldStatus = job.Status;
            job.Status = status;

            switch (status)
            {
                case JobStatus.Running:
                    if (!job.StartedAt.HasValue)
                        job.StartedAt = DateTime.UtcNow;
                    break;
                    
                case JobStatus.Success:
                case JobStatus.Failed:
                case JobStatus.Cancelled:
                case JobStatus.Timeout:
                    job.CompletedAt = DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(result))
                        job.Result = result;
                    if (!string.IsNullOrEmpty(error))
                        job.ErrorMessage = error;
                    break;
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

    public async Task<bool> AssignJobToAgentAsync(Guid jobId, string agentId)
    {
        await _jobsSemaphore.WaitAsync();
        try
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                return false;

            job.AssignedAgentId = agentId;
            job.Status = JobStatus.Assigned;
            job.AssignedAt = DateTime.UtcNow;

            _logger.LogInformation("Job {JobId} assigned to agent {AgentId}", jobId, agentId);
            
            return true;
        }
        finally
        {
            _jobsSemaphore.Release();
        }
    }

    public async Task<bool> CancelJobAsync(Guid jobId)
    {
        var success = await UpdateJobStatusAsync(jobId, JobStatus.Cancelled);
        if (success)
        {
            _logger.LogInformation("Job {JobId} cancelled", jobId);
        }
        return success;
    }

    public async Task<List<AutomationJob>> GetJobsByStatusAsync(JobStatus status)
    {
        await Task.CompletedTask;
        return _jobs.Values
            .Where(j => j.Status == status)
            .ToList();
    }

    public async Task<bool> RequeueJobAsync(Guid jobId)
    {
        await _jobsSemaphore.WaitAsync();
        try
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                return false;

            if (job.RetryCount >= job.MaxRetries)
            {
                await UpdateJobStatusAsync(jobId, JobStatus.Failed, 
                    error: $"Maximum retry attempts ({job.MaxRetries}) exceeded");
                return false;
            }

            job.RetryCount++;
            job.Status = JobStatus.Retry;
            job.AssignedAgentId = null;
            job.AssignedAt = null;
            job.StartedAt = null;
            job.ErrorMessage = null;

            // Re-enqueue with slightly lower priority to give other jobs a chance
            var newPriority = Math.Max(1, (int)job.Priority - 1);
            
            await _queueSemaphore.WaitAsync();
            try
            {
                _jobQueue.Enqueue(job.Id, -newPriority);
            }
            finally
            {
                _queueSemaphore.Release();
            }

            job.Status = JobStatus.Queued;
            job.QueuedAt = DateTime.UtcNow;

            _logger.LogInformation("Job {JobId} requeued for retry {RetryCount}/{MaxRetries}", 
                jobId, job.RetryCount, job.MaxRetries);

            return true;
        }
        finally
        {
            _jobsSemaphore.Release();
        }
    }

    public async Task<List<AutomationJob>> GetTimedOutJobsAsync(TimeSpan timeout)
    {
        await Task.CompletedTask;
        var cutoffTime = DateTime.UtcNow - timeout;
        
        return _jobs.Values
            .Where(j => j.Status == JobStatus.Running && 
                       j.StartedAt.HasValue && 
                       j.StartedAt.Value < cutoffTime)
            .ToList();
    }

    public async Task<Guid> ScheduleJobAsync(AutomationJob job)
    {
        await EnqueueJobAsync(job);
        return job.Id;
    }

    public async Task<int> GetJobCountByPriorityAsync(int priority)
    {
        await Task.CompletedTask;
        return _jobs.Values
            .Count(j => j.Status == JobStatus.Queued && (int)j.Priority == priority);
    }

    public async Task ProcessJobQueueAsync()
    {
        // Get available agents
        var agents = await _agentManager.GetAvailableAgentsAsync();
        if (agents.Count == 0)
        {
            _logger.LogDebug("No available agents to process jobs");
            return;
        }

        // Process jobs for available agents
        foreach (var agent in agents)
        {
            var job = await DequeueJobAsync();
            if (job == null)
                break; // No more jobs in queue

            _logger.LogInformation("Assigning job {JobId} '{JobName}' to agent {AgentId}",
                job.Id, job.Name, agent.Id);

            // Assign job to agent
            var assignedAgent = await _agentManager.AssignJobToAgentAsync(job);
            if (assignedAgent != null)
            {
                job.AssignedAgentId = assignedAgent.Id.ToString();
                job.Status = JobStatus.Running;
                job.StartedAt = DateTime.UtcNow;
                
                _logger.LogInformation("Job {JobId} started on agent {AgentId}",
                    job.Id, assignedAgent.Id);

                // In real implementation, would send job to agent via HTTP/gRPC
                await SimulateJobExecution(job, assignedAgent);
            }
            else
            {
                // Re-queue the job if assignment failed
                await EnqueueJobAsync(job);
                _logger.LogWarning("Failed to assign job {JobId} to agent, re-queuing", job.Id);
            }
        }
    }

    private async Task SimulateJobExecution(AutomationJob job, Agent agent)
    {
        // Simulate job execution in background
        _ = Task.Run(async () =>
        {
            try
            {
                // Simulate job execution time
                var executionTime = TimeSpan.FromSeconds(30 + new Random().Next(120)); // 30-150 seconds
                await Task.Delay(executionTime);

                // Simulate success/failure (90% success rate)
                var isSuccess = new Random().NextDouble() > 0.1;
                
                if (isSuccess)
                {
                    job.Status = JobStatus.Success;
                    job.Result = $"Job completed successfully on agent {agent.Name}";
                    _logger.LogInformation("Job {JobId} completed successfully", job.Id);
                }
                else
                {
                    job.Status = JobStatus.Failed;
                    job.ErrorMessage = "Simulated execution failure";
                    _logger.LogWarning("Job {JobId} failed: {ErrorMessage}", job.Id, job.ErrorMessage);
                }

                job.CompletedAt = DateTime.UtcNow;

                // Release the agent
                await _agentManager.ReleaseAgentAsync(agent.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during job {JobId} execution simulation", job.Id);
                job.Status = JobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                await _agentManager.ReleaseAgentAsync(agent.Id);
            }
        });

        await Task.CompletedTask;
    }
}