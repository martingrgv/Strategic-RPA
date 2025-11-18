using RPA.API.Models;

namespace RPA.API.Requests;

public class CreateAgentRequest
{
    public required string Name { get; set; }
    public required string WindowsUser { get; set; }
    public AgentCapabilities? Capabilities { get; set; }
}

public class CreateJobRequest
{
    public required string Name { get; set; }
    public required string ApplicationPath { get; set; }
    public string? Arguments { get; set; }
    public List<AutomationStepRequest> Steps { get; set; } = new();
    public JobPriority Priority { get; set; } = JobPriority.Normal;
    public string? WebhookUrl { get; set; }
}

public class AutomationStepRequest
{
    public int Order { get; set; }
    public StepType Type { get; set; }
    public required string Target { get; set; }
    public string? Value { get; set; }
    public int TimeoutMs { get; set; } = 5000;
    public bool ContinueOnError { get; set; } = false;
    public string? Description { get; set; }
}

public class UpdateAgentStatusRequest
{
    public AgentStatus Status { get; set; }
    public string? CurrentJobId { get; set; }
    public string? LastError { get; set; }
}
