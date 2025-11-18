namespace RPA.Orchestrator.Models;

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
    public DateTime? QueuedAt { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? AssignedAgentId { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Screenshots { get; set; } = new();
    public string? WebhookUrl { get; set; }
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue 
        ? CompletedAt.Value - StartedAt.Value 
        : null;
    public string? SubmittedBy { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
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
    Timeout,
    Retry
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
    public Dictionary<string, object> Parameters { get; set; } = new();
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
    KeyPress,
    SelectDropdown,
    ScrollTo,
    DragDrop
}