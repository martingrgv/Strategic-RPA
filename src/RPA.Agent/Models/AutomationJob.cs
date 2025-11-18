namespace RPA.Agent.Models;

public class AutomationJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string ApplicationPath { get; set; }
    public string? Arguments { get; set; }
    public List<AutomationStep> Steps { get; set; } = new();
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Screenshots { get; set; } = new();
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue 
        ? CompletedAt.Value - StartedAt.Value 
        : null;
}

public class AutomationStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public StepType Type { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string? ElementSelector { get; set; }
    public string? InputData { get; set; }
    public int TimeoutMs { get; set; } = 5000;
    public bool IsOptional { get; set; } = false;
    public string? Description { get; set; }
}

public enum JobStatus
{
    Pending,
    Running,
    Success,
    Failed,
    Cancelled,
    Timeout
}

public enum StepType
{
    Click,
    DoubleClick,
    RightClick,
    Type,
    KeyPress,
    WaitForElement,
    TakeScreenshot,
    GetText,
    SetText,
    SelectItem,
    DragDrop,
    Scroll,
    Validate,
    Custom
}

public class ExecutionResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorDetails { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public TimeSpan ExecutionTime { get; set; }
    public string? ScreenshotPath { get; set; }
}