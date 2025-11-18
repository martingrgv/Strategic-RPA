using RPA.Agent.Models;

namespace RPA.Agent.Services;

public interface IJobExecutor
{
    Task<ExecutionResult> ExecuteJobAsync(AutomationJob job);
    Task<bool> CancelJobAsync(Guid jobId);
    Task<AutomationJob?> GetJobStatusAsync(Guid jobId);
    bool IsJobRunning(Guid jobId);
}

public class JobExecutor : IJobExecutor
{
    private readonly ILogger<JobExecutor> _logger;
    private readonly IAutomationEngine _automationEngine;
    private readonly Dictionary<Guid, AutomationJob> _runningJobs = new();
    private readonly SemaphoreSlim _jobsSemaphore = new(1, 1);

    public JobExecutor(ILogger<JobExecutor> logger, IAutomationEngine automationEngine)
    {
        _logger = logger;
        _automationEngine = automationEngine;
    }

    public async Task<ExecutionResult> ExecuteJobAsync(AutomationJob job)
    {
        await _jobsSemaphore.WaitAsync();
        try
        {
            if (_runningJobs.ContainsKey(job.Id))
            {
                return new ExecutionResult
                {
                    Success = false,
                    Message = "Job is already running",
                    ErrorDetails = $"Job {job.Id} is currently being executed"
                };
            }

            _runningJobs[job.Id] = job;
        }
        finally
        {
            _jobsSemaphore.Release();
        }

        try
        {
            _logger.LogInformation("Starting job execution: {JobId} '{JobName}'", job.Id, job.Name);
            
            var result = await _automationEngine.ExecuteJobAsync(job);
            
            _logger.LogInformation("Job execution completed: {JobId} - Success: {Success}", 
                job.Id, result.Success);
                
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing job {JobId}", job.Id);
            
            return new ExecutionResult
            {
                Success = false,
                Message = "Job execution failed",
                ErrorDetails = ex.Message
            };
        }
        finally
        {
            await _jobsSemaphore.WaitAsync();
            try
            {
                _runningJobs.Remove(job.Id);
            }
            finally
            {
                _jobsSemaphore.Release();
            }
        }
    }

    public async Task<bool> CancelJobAsync(Guid jobId)
    {
        await _jobsSemaphore.WaitAsync();
        try
        {
            if (!_runningJobs.TryGetValue(jobId, out var job))
                return false;

            job.Status = JobStatus.Cancelled;
            _logger.LogInformation("Job {JobId} marked for cancellation", jobId);
            
            return true;
        }
        finally
        {
            _jobsSemaphore.Release();
        }
    }

    public async Task<AutomationJob?> GetJobStatusAsync(Guid jobId)
    {
        await Task.CompletedTask;
        _runningJobs.TryGetValue(jobId, out var job);
        return job;
    }

    public bool IsJobRunning(Guid jobId)
    {
        return _runningJobs.ContainsKey(jobId);
    }
}