using RPA.Orchestrator.Models;
using System.Diagnostics;

namespace RPA.Orchestrator.Services;

public interface IHealthMonitor
{
    Task<OrchestratorMetrics> GetMetricsAsync();
    Task<List<Agent>> CheckAgentHealthAsync();
    Task<List<RdpSession>> CheckSessionHealthAsync();
    Task<List<AutomationJob>> CheckJobHealthAsync();
    Task PerformHealthChecksAsync();
    Task CleanupResourcesAsync();
}

public class HealthMonitor : IHealthMonitor
{
    private readonly ILogger<HealthMonitor> _logger;
    private readonly IAgentManager _agentManager;
    private readonly ISessionManager _sessionManager;
    private readonly IJobScheduler _jobScheduler;
    private readonly IConfiguration _configuration;

    public HealthMonitor(
        ILogger<HealthMonitor> logger,
        IAgentManager agentManager,
        ISessionManager sessionManager,
        IJobScheduler jobScheduler,
        IConfiguration configuration)
    {
        _logger = logger;
        _agentManager = agentManager;
        _sessionManager = sessionManager;
        _jobScheduler = jobScheduler;
        _configuration = configuration;
    }

    public async Task<OrchestratorMetrics> GetMetricsAsync()
    {
        var agents = await _agentManager.GetAllAgentsAsync();
        var sessions = await _sessionManager.GetActiveSessionsAsync();
        var queuedJobs = await _jobScheduler.GetQueuedJobsAsync();
        var runningJobs = await _jobScheduler.GetJobsByStatusAsync(JobStatus.Running);
        var completedJobs = await _jobScheduler.GetJobsByStatusAsync(JobStatus.Success);
        var failedJobs = await _jobScheduler.GetJobsByStatusAsync(JobStatus.Failed);

        var metrics = new OrchestratorMetrics
        {
            TotalAgents = agents.Count,
            IdleAgents = agents.Count(a => a.Status == AgentStatus.Idle),
            BusyAgents = agents.Count(a => a.Status == AgentStatus.Busy),
            OfflineAgents = agents.Count(a => a.Status == AgentStatus.Offline),
            PendingJobs = await GetPendingJobsCountAsync(),
            QueuedJobs = queuedJobs.Count,
            RunningJobs = runningJobs.Count,
            CompletedJobs = completedJobs.Count,
            FailedJobs = failedJobs.Count,
            ActiveSessions = sessions.Count,
            TotalMemoryUsageMB = GetMemoryUsage(),
            AverageCpuUsage = GetCpuUsage()
        };

        // Calculate overall success rate
        var totalJobs = completedJobs.Count + failedJobs.Count;
        if (totalJobs > 0)
        {
            metrics.OverallSuccessRate = (double)completedJobs.Count / totalJobs;
        }

        // Calculate average job duration
        if (completedJobs.Count > 0)
        {
            var durations = completedJobs
                .Where(j => j.Duration.HasValue)
                .Select(j => j.Duration!.Value);
            
            if (durations.Any())
            {
                metrics.AverageJobDuration = TimeSpan.FromTicks((long)durations.Average(d => d.Ticks));
            }
        }

        return metrics;
    }

    public async Task<List<Agent>> CheckAgentHealthAsync()
    {
        var heartbeatTimeout = TimeSpan.FromMinutes(
            _configuration.GetValue<int>("HealthMonitor:AgentHeartbeatTimeoutMinutes", 5));

        var offlineAgents = await _agentManager.GetOfflineAgentsAsync(heartbeatTimeout);
        
        foreach (var agent in offlineAgents)
        {
            _logger.LogWarning("Agent {AgentId} '{AgentName}' appears offline - last heartbeat: {LastHeartbeat}", 
                agent.Id, agent.Name, agent.LastHeartbeat);
            
            await _agentManager.UpdateAgentStatusAsync(agent.Id, AgentStatus.Offline, 
                "No heartbeat received within timeout period");
        }

        return offlineAgents;
    }

    public async Task<List<RdpSession>> CheckSessionHealthAsync()
    {
        var sessions = await _sessionManager.GetActiveSessionsAsync();
        var unhealthySessions = new List<RdpSession>();

        foreach (var session in sessions)
        {
            // Check if session has been inactive for too long
            var inactivityTimeout = TimeSpan.FromHours(
                _configuration.GetValue<int>("HealthMonitor:SessionInactivityTimeoutHours", 2));

            if (session.LastActivity.HasValue && 
                DateTime.UtcNow - session.LastActivity.Value > inactivityTimeout)
            {
                _logger.LogWarning("Session {SessionId} has been inactive for {Duration}", 
                    session.SessionId, DateTime.UtcNow - session.LastActivity.Value);
                
                unhealthySessions.Add(session);
            }

            // Check sessions that need recycling
            var maxJobsPerSession = _configuration.GetValue<int>("HealthMonitor:MaxJobsPerSession", 50);
            if (session.JobsProcessed >= maxJobsPerSession)
            {
                _logger.LogInformation("Session {SessionId} has processed {JobCount} jobs and needs recycling", 
                    session.SessionId, session.JobsProcessed);
                
                unhealthySessions.Add(session);
            }
        }

        return unhealthySessions;
    }

    public async Task<List<AutomationJob>> CheckJobHealthAsync()
    {
        var jobTimeout = TimeSpan.FromMinutes(
            _configuration.GetValue<int>("HealthMonitor:JobTimeoutMinutes", 30));

        var timedOutJobs = await _jobScheduler.GetTimedOutJobsAsync(jobTimeout);
        
        foreach (var job in timedOutJobs)
        {
            _logger.LogWarning("Job {JobId} '{JobName}' has timed out after {Duration}", 
                job.Id, job.Name, DateTime.UtcNow - job.StartedAt);
            
            await _jobScheduler.UpdateJobStatusAsync(job.Id, JobStatus.Timeout, 
                error: $"Job timed out after {jobTimeout}");

            // Release the agent if it was assigned
            if (!string.IsNullOrEmpty(job.AssignedAgentId) && 
                Guid.TryParse(job.AssignedAgentId, out var agentId))
            {
                await _agentManager.ReleaseAgentAsync(agentId);
                _logger.LogInformation("Released agent {AgentId} from timed out job {JobId}", 
                    agentId, job.Id);
            }
        }

        return timedOutJobs;
    }

    public async Task PerformHealthChecksAsync()
    {
        _logger.LogDebug("Performing health checks...");

        try
        {
            // Check agent health
            var offlineAgents = await CheckAgentHealthAsync();
            
            // Check session health
            var unhealthySessions = await CheckSessionHealthAsync();
            
            // Check job health
            var timedOutJobs = await CheckJobHealthAsync();

            // Log summary
            if (offlineAgents.Count > 0 || unhealthySessions.Count > 0 || timedOutJobs.Count > 0)
            {
                _logger.LogInformation("Health check completed: {OfflineAgents} offline agents, {UnhealthySessions} unhealthy sessions, {TimedOutJobs} timed out jobs", 
                    offlineAgents.Count, unhealthySessions.Count, timedOutJobs.Count);
            }
            else
            {
                _logger.LogDebug("Health check completed: All systems healthy");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health checks");
        }
    }

    public async Task CleanupResourcesAsync()
    {
        _logger.LogDebug("Performing resource cleanup...");

        try
        {
            // Clean up old completed jobs (keep last 1000)
            var allCompletedJobs = await _jobScheduler.GetJobsByStatusAsync(JobStatus.Success);
            var allFailedJobs = await _jobScheduler.GetJobsByStatusAsync(JobStatus.Failed);
            var allJobs = allCompletedJobs.Concat(allFailedJobs)
                .OrderByDescending(j => j.CompletedAt)
                .ToList();

            var maxCompletedJobs = _configuration.GetValue<int>("HealthMonitor:MaxCompletedJobsToKeep", 1000);
            if (allJobs.Count > maxCompletedJobs)
            {
                var jobsToCleanup = allJobs.Skip(maxCompletedJobs);
                foreach (var job in jobsToCleanup)
                {
                    // In real implementation, would remove from persistent storage
                    _logger.LogDebug("Cleaning up old job {JobId} from {CompletedAt}", 
                        job.Id, job.CompletedAt);
                }
            }

            // Clean up orphaned sessions
            var sessions = await _sessionManager.GetActiveSessionsAsync();
            var agents = await _agentManager.GetAllAgentsAsync();
            var agentSessionIds = agents.Select(a => a.SessionId).ToHashSet();

            foreach (var session in sessions)
            {
                if (!agentSessionIds.Contains(session.SessionId))
                {
                    _logger.LogWarning("Found orphaned session {SessionId}, terminating", 
                        session.SessionId);
                    await _sessionManager.TerminateSessionAsync(session.SessionId);
                }
            }

            _logger.LogDebug("Resource cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during resource cleanup");
        }
    }

    private async Task<int> GetPendingJobsCountAsync()
    {
        var pendingJobs = await _jobScheduler.GetJobsByStatusAsync(JobStatus.Pending);
        return pendingJobs.Count;
    }

    private long GetMemoryUsage()
    {
        var process = Process.GetCurrentProcess();
        return process.WorkingSet64 / 1024 / 1024; // Convert to MB
    }

    private double GetCpuUsage()
    {
        // Simplified CPU usage - in real implementation would use performance counters
        // or more sophisticated monitoring
        return Environment.ProcessorCount * 50.0; // Placeholder
    }
}