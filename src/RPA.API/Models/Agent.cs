namespace RPA.API.Models;

public class Agent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string SessionId { get; set; }
    public required string WindowsUser { get; set; }
    public AgentStatus Status { get; set; } = AgentStatus.Idle;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastHeartbeat { get; set; }
    public string? CurrentJobId { get; set; }
    public int JobsExecuted { get; set; } = 0;
    public string? LastError { get; set; }
    public AgentCapabilities Capabilities { get; set; } = new();
}

public enum AgentStatus
{
    Starting,
    Idle,
    Busy,
    Error,
    Offline,
    Terminating
}

public class AgentCapabilities
{
    public List<string> SupportedApplications { get; set; } = new();
    public int MaxConcurrentJobs { get; set; } = 1;
    public bool SupportsScreenCapture { get; set; } = true;
    public bool SupportsFlaUI { get; set; } = true;
}