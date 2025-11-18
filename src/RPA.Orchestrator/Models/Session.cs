namespace RPA.Orchestrator.Models;

public class RdpSession
{
    public required string SessionId { get; set; }
    public required string WindowsUser { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Creating;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TerminatedAt { get; set; }
    public string? AssignedAgentId { get; set; }
    public int JobsProcessed { get; set; } = 0;
    public DateTime? LastActivity { get; set; }
    public DateTime? LastHealthCheck { get; set; }
    public string? ServerHost { get; set; } = Environment.MachineName;
    public int Port { get; set; }
    public SessionMetrics Metrics { get; set; } = new();
}

public enum SessionStatus
{
    Creating,
    Starting,
    Active,
    Idle,
    Busy,
    Recycling,
    Terminating,
    Terminated,
    Unhealthy,
    Error
}

public class SessionMetrics
{
    public TimeSpan TotalUptime => DateTime.UtcNow - CreatedAt;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int TotalJobs { get; set; }
    public TimeSpan AverageJobDuration { get; set; }
    public long MemoryUsageMB { get; set; }
    public double CpuUsagePercent { get; set; }
}