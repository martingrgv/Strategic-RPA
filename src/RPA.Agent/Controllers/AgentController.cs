using Microsoft.AspNetCore.Mvc;
using RPA.Agent.Models;
using RPA.Agent.Services;

namespace RPA.Agent.Controllers;

[ApiController]
[Route("api/agent")]
public class AgentController : ControllerBase
{
    private readonly IJobExecutor _jobExecutor;
    private readonly IApiReportingService _apiReportingService;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IJobExecutor jobExecutor, 
        IApiReportingService apiReportingService,
        ILogger<AgentController> logger)
    {
        _jobExecutor = jobExecutor;
        _apiReportingService = apiReportingService;
        _logger = logger;
    }

    /// <summary>
    /// Receive job from orchestrator
    /// </summary>
    [HttpPost("jobs")]
    public async Task<IActionResult> ReceiveJob([FromBody] AutomationJob job)
    {
        try
        {
            _logger.LogInformation("Received job {JobId} from orchestrator", job.Id);
            
            // Execute job asynchronously
            _ = Task.Run(async () =>
            {
                var result = await _jobExecutor.ExecuteJobAsync(job);
                
                // Report completion back to API
                var status = result.Success ? JobStatus.Success : JobStatus.Failed;
                await _apiReportingService.ReportJobStatusAsync(
                    job.Id, 
                    status, 
                    result.Message, 
                    result.Success ? null : result.ErrorDetails);
            });

            return Ok(new { Message = "Job accepted", JobId = job.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to receive job {JobId}", job?.Id);
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Cancel running job
    /// </summary>
    [HttpPost("jobs/{jobId:guid}/cancel")]
    public async Task<IActionResult> CancelJob(Guid jobId)
    {
        try
        {
            var success = await _jobExecutor.CancelJobAsync(jobId);
            return Ok(new { Success = success });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel job {JobId}", jobId);
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Get agent status
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new 
        { 
            Status = "Idle",
            Timestamp = DateTime.UtcNow,
            MachineName = Environment.MachineName
        });
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        return Ok(new 
        { 
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0"
        });
    }

    /// <summary>
    /// Graceful shutdown endpoint
    /// </summary>
    [HttpPost("shutdown")]
    public IActionResult Shutdown()
    {
        try
        {
            _logger.LogInformation("Agent shutdown requested");
            
            // Trigger graceful shutdown
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000); // Give time to respond
                Environment.Exit(0);
            });
            
            return Ok(new { Message = "Shutdown initiated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate shutdown");
            return BadRequest(new { Message = ex.Message });
        }
    }
}