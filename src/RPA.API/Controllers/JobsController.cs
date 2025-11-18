using Microsoft.AspNetCore.Mvc;
using RPA.API.Models;
using RPA.API.Requests;
using RPA.API.Responses;
using RPA.API.Services;

namespace RPA.API.Controllers;

[ApiController]
[Route("api/automation/[controller]")]
[Produces("application/json")]
public class JobsController : ControllerBase
{
    private readonly IJobService _jobService;
    private readonly IAgentService _agentService;
    private readonly ILogger<JobsController> _logger;

    public JobsController(IJobService jobService, IAgentService agentService, ILogger<JobsController> logger)
    {
        _jobService = jobService;
        _agentService = agentService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new automation job
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<JobResponse>>> CreateJob([FromBody] CreateJobRequest request)
    {
        try
        {
            _logger.LogInformation("Creating job {JobName} for application {ApplicationPath}", 
                request.Name, request.ApplicationPath);

            // Convert request steps to domain model
            var steps = request.Steps.Select(s => new AutomationStep
            {
                Order = s.Order,
                Type = s.Type,
                Target = s.Target,
                Value = s.Value,
                TimeoutMs = s.TimeoutMs,
                ContinueOnError = s.ContinueOnError,
                Description = s.Description
            }).ToList();

            var job = await _jobService.CreateJobAsync(
                request.Name, 
                request.ApplicationPath, 
                request.Arguments, 
                steps, 
                request.Priority, 
                request.WebhookUrl);

            // Try to assign to an available agent immediately
            var agent = await _agentService.AssignJobToAgentAsync(job.Id, request.ApplicationPath);
            if (agent != null)
            {
                await _jobService.AssignJobAsync(job.Id, agent.Id.ToString());
                _logger.LogInformation("Job {JobId} immediately assigned to agent {AgentId}", job.Id, agent.Id);
            }

            var response = JobResponse.FromJob(job);
            return CreatedAtAction(nameof(GetJob), new { id = job.Id }, ApiResponse<JobResponse>.SuccessResult(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create job {JobName}", request.Name);
            return StatusCode(500, ApiResponse<JobResponse>.ErrorResult("Failed to create job"));
        }
    }

    /// <summary>
    /// Get job by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<JobResponse>>> GetJob(Guid id)
    {
        try
        {
            var job = await _jobService.GetJobAsync(id);
            if (job == null)
                return NotFound(ApiResponse<JobResponse>.ErrorResult($"Job {id} not found"));

            var response = JobResponse.FromJob(job);
            return Ok(ApiResponse<JobResponse>.SuccessResult(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get job {JobId}", id);
            return StatusCode(500, ApiResponse<JobResponse>.ErrorResult("Failed to retrieve job"));
        }
    }

    /// <summary>
    /// Get jobs with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<JobResponse>>>> GetJobs(
        [FromQuery] JobStatus? status = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        try
        {
            var jobs = await _jobService.GetJobsAsync(status, skip, Math.Min(take, 100));
            var responses = jobs.Select(JobResponse.FromJob).ToList();
            
            return Ok(ApiResponse<List<JobResponse>>.SuccessResult(responses));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get jobs");
            return StatusCode(500, ApiResponse<List<JobResponse>>.ErrorResult("Failed to retrieve jobs"));
        }
    }

    /// <summary>
    /// Update job status (typically called by agents)
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateJobStatus(Guid id, [FromBody] UpdateJobStatusRequest request)
    {
        try
        {
            var success = await _jobService.UpdateJobStatusAsync(id, request.Status, request.Result, request.ErrorMessage);
            if (!success)
                return NotFound(ApiResponse<bool>.ErrorResult($"Job {id} not found"));

            // If job completed, release the agent
            if (request.Status is JobStatus.Success or JobStatus.Failed or JobStatus.Cancelled)
            {
                var job = await _jobService.GetJobAsync(id);
                if (job?.AssignedAgentId != null && Guid.TryParse(job.AssignedAgentId, out var agentId))
                {
                    await _agentService.ReleaseAgentAsync(agentId);
                    _logger.LogInformation("Released agent {AgentId} after job {JobId} completion", agentId, id);
                }
            }

            return Ok(ApiResponse<bool>.SuccessResult(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update job {JobId} status", id);
            return StatusCode(500, ApiResponse<bool>.ErrorResult("Failed to update job status"));
        }
    }

    /// <summary>
    /// Add screenshot to job
    /// </summary>
    [HttpPost("{id:guid}/screenshots")]
    public async Task<ActionResult<ApiResponse<bool>>> AddScreenshot(Guid id, [FromBody] AddScreenshotRequest request)
    {
        try
        {
            var success = await _jobService.AddScreenshotAsync(id, request.ScreenshotPath);
            if (!success)
                return NotFound(ApiResponse<bool>.ErrorResult($"Job {id} not found"));

            return Ok(ApiResponse<bool>.SuccessResult(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add screenshot to job {JobId}", id);
            return StatusCode(500, ApiResponse<bool>.ErrorResult("Failed to add screenshot"));
        }
    }

    /// <summary>
    /// Cancel job
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<bool>>> CancelJob(Guid id)
    {
        try
        {
            var success = await _jobService.CancelJobAsync(id);
            if (!success)
                return NotFound(ApiResponse<bool>.ErrorResult($"Job {id} not found"));

            // Release agent if assigned
            var job = await _jobService.GetJobAsync(id);
            if (job?.AssignedAgentId != null && Guid.TryParse(job.AssignedAgentId, out var agentId))
            {
                await _agentService.ReleaseAgentAsync(agentId);
                _logger.LogInformation("Released agent {AgentId} after job {JobId} cancellation", agentId, id);
            }

            return Ok(ApiResponse<bool>.SuccessResult(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel job {JobId}", id);
            return StatusCode(500, ApiResponse<bool>.ErrorResult("Failed to cancel job"));
        }
    }
}

public class UpdateJobStatusRequest
{
    public JobStatus Status { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AddScreenshotRequest
{
    public required string ScreenshotPath { get; set; }
}