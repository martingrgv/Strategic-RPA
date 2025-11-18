namespace RPA.API.Models;

public class AutomationJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string ApplicationPath { get; set; }
    public string? Arguments { get; set; }
    public List<AutomationStep> Steps { get; set; } = new();
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public JobPriority Priority { get; set; } = JobPriority.Normal;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? AssignedAgentId { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Screenshots { get; set; } = new();
    public string? WebhookUrl { get; set; }
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue 
        ? CompletedAt.Value - StartedAt.Value 
        : null;
}

public enum JobStatus
{
    Pending,
    Queued,
    Assigned,
    Running,
    Success,
    Failed,
    Cancelled,
    Timeout
}

public enum JobPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}

public class AutomationStep
{
    public int Order { get; set; }
    public StepType Type { get; set; }
    public required string Target { get; set; }
    public string? Value { get; set; }
    public int TimeoutMs { get; set; } = 5000;
    public bool ContinueOnError { get; set; } = false;
    public string? Description { get; set; }
}

public enum StepType
{
    Click,
    Type,
    Wait,
    Validate,
    Screenshot,
    GetText,
    SetFocus,
    KeyPress
}