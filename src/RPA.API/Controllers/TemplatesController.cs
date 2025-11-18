using Microsoft.AspNetCore.Mvc;
using RPA.API.Models;
using RPA.API.Responses;
using RPA.API.Services;

namespace RPA.API.Controllers;

[ApiController]
[Route("api/automation/[controller]")]
[Produces("application/json")]
public class TemplatesController : ControllerBase
{
    private readonly IAutomationTemplateService _templateService;
    private readonly IJobService _jobService;
    private readonly IAgentService _agentService;
    private readonly ILogger<TemplatesController> _logger;

    public TemplatesController(
        IAutomationTemplateService templateService,
        IJobService jobService,
        IAgentService agentService,
        ILogger<TemplatesController> logger)
    {
        _templateService = templateService;
        _jobService = jobService;
        _agentService = agentService;
        _logger = logger;
    }

    /// <summary>
    /// Get all available automation templates
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<AutomationTemplate>>>> GetTemplates()
    {
        try
        {
            var templates = await _templateService.GetAvailableTemplatesAsync();
            return Ok(ApiResponse<List<AutomationTemplate>>.SuccessResult(templates));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get automation templates");
            return StatusCode(500, ApiResponse<List<AutomationTemplate>>.ErrorResult("Failed to retrieve templates"));
        }
    }

    /// <summary>
    /// Get specific automation template by ID
    /// </summary>
    [HttpGet("{templateId}")]
    public async Task<ActionResult<ApiResponse<AutomationTemplate>>> GetTemplate(string templateId)
    {
        try
        {
            var template = await _templateService.GetTemplateAsync(templateId);
            if (template == null)
                return NotFound(ApiResponse<AutomationTemplate>.ErrorResult($"Template '{templateId}' not found"));

            return Ok(ApiResponse<AutomationTemplate>.SuccessResult(template));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get template {TemplateId}", templateId);
            return StatusCode(500, ApiResponse<AutomationTemplate>.ErrorResult("Failed to retrieve template"));
        }
    }

    /// <summary>
    /// Execute an automation template with parameters
    /// </summary>
    [HttpPost("{templateId}/execute")]
    public async Task<ActionResult<ApiResponse<JobResponse>>> ExecuteTemplate(
        string templateId, 
        [FromBody] ExecuteTemplateRequest request)
    {
        try
        {
            _logger.LogInformation("Executing template {TemplateId} with parameters: {Parameters}", 
                templateId, request.Parameters);

            // Create job from template
            var job = await _templateService.CreateJobFromTemplateAsync(templateId, request.Parameters);
            
            // Save the job
            var savedJob = await _jobService.CreateJobAsync(
                job.Name,
                job.ApplicationPath,
                job.Arguments,
                job.Steps,
                request.Priority,
                request.WebhookUrl);

            // Set template information
            savedJob.TemplateId = templateId;
            savedJob.TemplateParameters = request.Parameters;

            // Try to assign to an available agent immediately
            var agent = await _agentService.AssignJobToAgentAsync(savedJob.Id, job.ApplicationPath);
            if (agent != null)
            {
                await _jobService.AssignJobAsync(savedJob.Id, agent.Id.ToString());
                _logger.LogInformation("Template job {JobId} immediately assigned to agent {AgentId}", 
                    savedJob.Id, agent.Id);
            }

            var response = JobResponse.FromJob(savedJob);
            return CreatedAtAction(nameof(GetExecutionResult), new { jobId = savedJob.Id }, 
                ApiResponse<JobResponse>.SuccessResult(response));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid template execution request for {TemplateId}", templateId);
            return BadRequest(ApiResponse<JobResponse>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute template {TemplateId}", templateId);
            return StatusCode(500, ApiResponse<JobResponse>.ErrorResult("Failed to execute template"));
        }
    }

    /// <summary>
    /// Get the result of a template execution
    /// </summary>
    [HttpGet("executions/{jobId:guid}")]
    public async Task<ActionResult<ApiResponse<JobResponse>>> GetExecutionResult(Guid jobId)
    {
        try
        {
            var job = await _jobService.GetJobAsync(jobId);
            if (job == null)
                return NotFound(ApiResponse<JobResponse>.ErrorResult($"Execution {jobId} not found"));

            var response = JobResponse.FromJob(job);
            return Ok(ApiResponse<JobResponse>.SuccessResult(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get execution result {JobId}", jobId);
            return StatusCode(500, ApiResponse<JobResponse>.ErrorResult("Failed to retrieve execution result"));
        }
    }
}

public class ExecuteTemplateRequest
{
    public Dictionary<string, object> Parameters { get; set; } = new();
    public JobPriority Priority { get; set; } = JobPriority.Normal;
    public string? WebhookUrl { get; set; }
}