using Microsoft.AspNetCore.Mvc;
using RPA.Orchestrator.Models;
using RPA.Orchestrator.Services;
using System.Text.Json;

namespace RPA.Orchestrator.Controllers;

[ApiController]
[Route("api/orchestrator")]
public class OrchestratorController : ControllerBase
{
    private readonly IJobScheduler _jobScheduler;
    private readonly IAgentManager _agentManager;
    private readonly IAgentCommunicationService _agentCommunication;
    private readonly ILogger<OrchestratorController> _logger;

    public OrchestratorController(
        IJobScheduler jobScheduler,
        IAgentManager agentManager,
        IAgentCommunicationService agentCommunication,
        ILogger<OrchestratorController> logger)
    {
        _jobScheduler = jobScheduler;
        _agentManager = agentManager;
        _agentCommunication = agentCommunication;
        _logger = logger;
    }

    /// <summary>
    /// Receive job from API via message queue
    /// </summary>
    [HttpPost("jobs")]
    public async Task<IActionResult> ReceiveJob([FromBody] JobMessage jobMessage)
    {
        try
        {
            _logger.LogInformation("Received job {JobId} of type {MessageType} from API", 
                jobMessage.JobId, jobMessage.Type);

            if (jobMessage.Type == "JobCreated")
            {
                // Convert from API model to Orchestrator model
                var orchestratorJob = ConvertToOrchestratorJob(jobMessage.Job);
                
                // Schedule the job
                var scheduledJobId = await _jobScheduler.ScheduleJobAsync(orchestratorJob);
                
                // Try to find available agent and assign
                var availableAgent = await _agentManager.AssignJobToAgentAsync(orchestratorJob);
                if (availableAgent != null)
                {
                    // Send job to agent
                    var success = await _agentCommunication.SendJobToAgentAsync(availableAgent, orchestratorJob);
                    if (success)
                    {
                        await _jobScheduler.UpdateJobStatusAsync(scheduledJobId, JobStatus.Running);
                        _logger.LogInformation("Job {JobId} assigned to agent {AgentId} and sent successfully", 
                            jobMessage.JobId, availableAgent.Id);
                    }
                    else
                    {
                        await _jobScheduler.UpdateJobStatusAsync(scheduledJobId, JobStatus.Queued);
                        _logger.LogWarning("Failed to send job {JobId} to agent {AgentId}, job remains queued", 
                            jobMessage.JobId, availableAgent.Id);
                    }
                }
                else
                {
                    _logger.LogInformation("No available agents for job {JobId}, job queued", jobMessage.JobId);
                }
            }
            else if (jobMessage.Type == "JobCancelled")
            {
                await _jobScheduler.CancelJobAsync(jobMessage.JobId);
                _logger.LogInformation("Job {JobId} cancelled", jobMessage.JobId);
            }

            return Ok(new { Message = "Job message processed", JobId = jobMessage.JobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process job message {JobId}", jobMessage.JobId);
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Cancel job
    /// </summary>
    [HttpPost("jobs/{jobId:guid}/cancel")]
    public async Task<IActionResult> CancelJob(Guid jobId)
    {
        try
        {
            var success = await _jobScheduler.CancelJobAsync(jobId);
            return Ok(new { Success = success });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel job {JobId}", jobId);
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Get orchestrator status
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            var agents = await _agentManager.GetAllAgentsAsync();
            var queuedJobs = await _jobScheduler.GetQueuedJobsAsync();
            
            return Ok(new 
            { 
                Status = "Healthy",
                AgentsCount = agents.Count,
                QueuedJobsCount = queuedJobs.Count,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get orchestrator status");
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    private AutomationJob ConvertToOrchestratorJob(object jobData)
    {
        // Convert from API AutomationJob to Orchestrator AutomationJob
        // This handles the model differences between projects
        var json = JsonSerializer.Serialize(jobData);
        var orchestratorJob = JsonSerializer.Deserialize<AutomationJob>(json);
        
        return orchestratorJob ?? throw new InvalidOperationException("Failed to convert job data");
    }
}

public class JobMessage
{
    public string Type { get; set; } = string.Empty; // "JobCreated", "JobCancelled"
    public Guid JobId { get; set; }
    public object? Job { get; set; }
    public DateTime Timestamp { get; set; }
}