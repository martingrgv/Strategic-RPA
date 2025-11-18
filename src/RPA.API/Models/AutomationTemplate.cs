using RPA.API.Models;

namespace RPA.API.Models;

public class AutomationTemplate
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string ApplicationPath { get; set; }
    public string? Arguments { get; set; }
    public List<TemplateParameter> Parameters { get; set; } = new();
    public List<AutomationStepTemplate> StepTemplate { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class TemplateParameter
{
    public required string Name { get; set; }
    public required string Type { get; set; } // "string", "number", "boolean"
    public bool Required { get; set; } = false;
    public string? Description { get; set; }
    public object? DefaultValue { get; set; }
    public string? ValidationPattern { get; set; }
}

public class AutomationStepTemplate
{
    public int Order { get; set; }
    public StepType Type { get; set; }
    public string Target { get; set; } = string.Empty;
    public string? Value { get; set; }
    public int TimeoutMs { get; set; } = 5000;
    public bool ContinueOnError { get; set; } = false;
    public string? Description { get; set; }
}