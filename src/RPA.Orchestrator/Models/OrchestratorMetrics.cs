namespace RPA.Orchestrator.Models;

public class OrchestratorMetrics
{
    public int TotalAgents { get; set; }
    public int IdleAgents { get; set; }
    public int BusyAgents { get; set; }
    public int OfflineAgents { get; set; }
    public int PendingJobs { get; set; }
    public int QueuedJobs { get; set; }
    public int RunningJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public int ActiveSessions { get; set; }
    public long TotalMemoryUsageMB { get; set; }
    public double AverageCpuUsage { get; set; }
    public double OverallSuccessRate { get; set; }
    public TimeSpan? AverageJobDuration { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}