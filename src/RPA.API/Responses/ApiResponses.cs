using RPA.API.Models;

namespace RPA.API.Responses;

public class AgentResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string WindowsUser { get; set; } = string.Empty;
    public AgentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public string? CurrentJobId { get; set; }
    public int JobsExecuted { get; set; }
    public string? LastError { get; set; }
    public AgentCapabilities Capabilities { get; set; } = new();

    public static AgentResponse FromAgent(Agent agent)
    {
        return new AgentResponse
        {
            Id = agent.Id,
            Name = agent.Name,
            SessionId = agent.SessionId,
            WindowsUser = agent.WindowsUser,
            Status = agent.Status,
            CreatedAt = agent.CreatedAt,
            LastHeartbeat = agent.LastHeartbeat,
            CurrentJobId = agent.CurrentJobId,
            JobsExecuted = agent.JobsExecuted,
            LastError = agent.LastError,
            Capabilities = agent.Capabilities
        };
    }
}

public class JobResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ApplicationPath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public List<AutomationStepResponse> Steps { get; set; } = new();
    public JobStatus Status { get; set; }
    public JobPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? AssignedAgentId { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Screenshots { get; set; } = new();
    public TimeSpan? Duration { get; set; }

    public static JobResponse FromJob(AutomationJob job)
    {
        return new JobResponse
        {
            Id = job.Id,
            Name = job.Name,
            ApplicationPath = job.ApplicationPath,
            Arguments = job.Arguments,
            Steps = job.Steps.Select(AutomationStepResponse.FromStep).ToList(),
            Status = job.Status,
            Priority = job.Priority,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            AssignedAgentId = job.AssignedAgentId,
            Result = job.Result,
            ErrorMessage = job.ErrorMessage,
            Screenshots = job.Screenshots,
            Duration = job.Duration
        };
    }
}

public class AutomationStepResponse
{
    public int Order { get; set; }
    public StepType Type { get; set; }
    public string Target { get; set; } = string.Empty;
    public string? Value { get; set; }
    public int TimeoutMs { get; set; }
    public bool ContinueOnError { get; set; }
    public string? Description { get; set; }

    public static AutomationStepResponse FromStep(AutomationStep step)
    {
        return new AutomationStepResponse
        {
            Order = step.Order,
            Type = step.Type,
            Target = step.Target,
            Value = step.Value,
            TimeoutMs = step.TimeoutMs,
            ContinueOnError = step.ContinueOnError,
            Description = step.Description
        };
    }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ApiResponse<T> SuccessResult(T data)
    {
        return new ApiResponse<T> { Success = true, Data = data };
    }

    public static ApiResponse<T> ErrorResult(string error)
    {
        return new ApiResponse<T> { Success = false, ErrorMessage = error };
    }

    public static ApiResponse<T> ErrorResult(List<string> errors)
    {
        return new ApiResponse<T> { Success = false, Errors = errors };
    }
}