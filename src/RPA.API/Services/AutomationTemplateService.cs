using RPA.API.Models;

namespace RPA.API.Services;

public interface IAutomationTemplateService
{
    Task<List<AutomationTemplate>> GetAvailableTemplatesAsync();
    Task<AutomationTemplate?> GetTemplateAsync(string templateId);
    Task<AutomationJob> CreateJobFromTemplateAsync(string templateId, Dictionary<string, object>? parameters = null);
}

public class AutomationTemplateService : IAutomationTemplateService
{
    private readonly ILogger<AutomationTemplateService> _logger;
    private readonly Dictionary<string, AutomationTemplate> _templates;

    public AutomationTemplateService(ILogger<AutomationTemplateService> logger)
    {
        _logger = logger;
        _templates = InitializeTemplates();
    }

    private Dictionary<string, AutomationTemplate> InitializeTemplates()
    {
        return new Dictionary<string, AutomationTemplate>
        {
            ["CalculatorAddition"] = new AutomationTemplate
            {
                Id = "CalculatorAddition",
                Name = "Calculator Addition",
                Description = "Performs addition operation using Windows Calculator",
                ApplicationPath = "calc.exe",
                Parameters = new List<TemplateParameter>
                {
                    new() { Name = "num1", Type = "number", Required = true, Description = "First number" },
                    new() { Name = "num2", Type = "number", Required = true, Description = "Second number" }
                },
                StepTemplate = new List<AutomationStepTemplate>
                {
                    new() { Order = 1, Type = StepType.Click, Target = "Button[@Name='{num1}']", Description = "Click first number" },
                    new() { Order = 2, Type = StepType.Click, Target = "Button[@Name='+']", Description = "Click plus operator" },
                    new() { Order = 3, Type = StepType.Click, Target = "Button[@Name='{num2}']", Description = "Click second number" },
                    new() { Order = 4, Type = StepType.Click, Target = "Button[@Name='=']", Description = "Click equals" },
                    new() { Order = 5, Type = StepType.Validate, Target = "Text[@Name='Display']", Value = "{result}", Description = "Validate result" }
                }
            },

            ["CalculatorMultiplication"] = new AutomationTemplate
            {
                Id = "CalculatorMultiplication",
                Name = "Calculator Multiplication", 
                Description = "Performs multiplication using Windows Calculator",
                ApplicationPath = "calc.exe",
                Parameters = new List<TemplateParameter>
                {
                    new() { Name = "num1", Type = "number", Required = true, Description = "First number" },
                    new() { Name = "num2", Type = "number", Required = true, Description = "Second number" }
                },
                StepTemplate = new List<AutomationStepTemplate>
                {
                    new() { Order = 1, Type = StepType.Click, Target = "Button[@Name='{num1}']", Description = "Click first number" },
                    new() { Order = 2, Type = StepType.Click, Target = "Button[@Name='×']", Description = "Click multiply operator" },
                    new() { Order = 3, Type = StepType.Click, Target = "Button[@Name='{num2}']", Description = "Click second number" },
                    new() { Order = 4, Type = StepType.Click, Target = "Button[@Name='=']", Description = "Click equals" },
                    new() { Order = 5, Type = StepType.Validate, Target = "Text[@Name='Display']", Value = "{result}", Description = "Validate result" }
                }
            },

            ["NotepadTextEntry"] = new AutomationTemplate
            {
                Id = "NotepadTextEntry",
                Name = "Notepad Text Entry",
                Description = "Opens Notepad and enters specified text",
                ApplicationPath = "notepad.exe",
                Parameters = new List<TemplateParameter>
                {
                    new() { Name = "text", Type = "string", Required = true, Description = "Text to enter" },
                    new() { Name = "saveAs", Type = "string", Required = false, Description = "Optional filename to save as" }
                },
                StepTemplate = new List<AutomationStepTemplate>
                {
                    new() { Order = 1, Type = StepType.Wait, Value = "2000", Description = "Wait for application to load" },
                    new() { Order = 2, Type = StepType.Type, Target = "Document", Value = "{text}", Description = "Enter text" },
                    new() { Order = 3, Type = StepType.Click, Target = "MenuItem[@Name='File']", Description = "Open File menu" },
                    new() { Order = 4, Type = StepType.Click, Target = "MenuItem[@Name='Save As...']", Description = "Click Save As" }
                }
            }
        };
    }

    public async Task<List<AutomationTemplate>> GetAvailableTemplatesAsync()
    {
        await Task.CompletedTask;
        return _templates.Values.ToList();
    }

    public async Task<AutomationTemplate?> GetTemplateAsync(string templateId)
    {
        await Task.CompletedTask;
        _templates.TryGetValue(templateId, out var template);
        return template;
    }

    public async Task<AutomationJob> CreateJobFromTemplateAsync(string templateId, Dictionary<string, object>? parameters = null)
    {
        var template = await GetTemplateAsync(templateId);
        if (template == null)
            throw new ArgumentException($"Template '{templateId}' not found");

        parameters ??= new Dictionary<string, object>();

        // Validate required parameters
        foreach (var param in template.Parameters.Where(p => p.Required))
        {
            if (!parameters.ContainsKey(param.Name))
                throw new ArgumentException($"Required parameter '{param.Name}' is missing");
        }

        // Calculate result for math operations
        if (templateId.StartsWith("Calculator"))
        {
            var num1 = Convert.ToDouble(parameters["num1"]);
            var num2 = Convert.ToDouble(parameters["num2"]);
            var result = templateId == "CalculatorAddition" ? num1 + num2 : num1 * num2;
            parameters["result"] = result.ToString();
        }

        // Generate steps from template
        var steps = template.StepTemplate.Select(stepTemplate => new AutomationStep
        {
            Order = stepTemplate.Order,
            Type = stepTemplate.Type,
            Target = ReplaceParameters(stepTemplate.Target, parameters) ?? stepTemplate.Target,
            Value = ReplaceParameters(stepTemplate.Value, parameters),
            TimeoutMs = stepTemplate.TimeoutMs,
            ContinueOnError = stepTemplate.ContinueOnError,
            Description = ReplaceParameters(stepTemplate.Description, parameters)
        }).ToList();

        var jobName = $"{template.Name} - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
        
        return new AutomationJob
        {
            Name = jobName,
            ApplicationPath = template.ApplicationPath,
            Arguments = template.Arguments,
            Steps = steps,
            Priority = JobPriority.Normal,
            Status = JobStatus.Pending,
            TemplateId = templateId,
            TemplateParameters = parameters
        };
    }

    private string? ReplaceParameters(string? text, Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = text;
        foreach (var param in parameters)
        {
            result = result.Replace($"{{{param.Key}}}", param.Value?.ToString());
        }
        return result;
    }
}