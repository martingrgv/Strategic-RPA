using Microsoft.AspNetCore.Mvc;
using RPA.API.Models;
using RPA.API.Requests;
using RPA.API.Responses;
using RPA.API.Services;

namespace RPA.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AgentsController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(IAgentService agentService, ILogger<AgentsController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new automation agent
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<AgentResponse>>> CreateAgent([FromBody] CreateAgentRequest request)
    {
        try
        {
            _logger.LogInformation("Creating agent {AgentName} for user {WindowsUser}", request.Name, request.WindowsUser);
            
            var agent = await _agentService.CreateAgentAsync(request.Name, request.WindowsUser, request.Capabilities);
            var response = AgentResponse.FromAgent(agent);
            
            return Ok(ApiResponse<AgentResponse>.SuccessResult(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create agent {AgentName}", request.Name);
            return StatusCode(500, ApiResponse<AgentResponse>.ErrorResult("Failed to create agent"));
        }
    }

    /// <summary>
    /// Get all agents
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<AgentResponse>>>> GetAgents()
    {
        try
        {
            var agents = await _agentService.GetAllAgentsAsync();
            var responses = agents.Select(AgentResponse.FromAgent).ToList();
            
            return Ok(ApiResponse<List<AgentResponse>>.SuccessResult(responses));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agents");
            return StatusCode(500, ApiResponse<List<AgentResponse>>.ErrorResult("Failed to retrieve agents"));
        }
    }

    /// <summary>
    /// Get agent by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<AgentResponse>>> GetAgent(Guid id)
    {
        try
        {
            var agent = await _agentService.GetAgentAsync(id);
            if (agent == null)
                return NotFound(ApiResponse<AgentResponse>.ErrorResult($"Agent {id} not found"));

            var response = AgentResponse.FromAgent(agent);
            return Ok(ApiResponse<AgentResponse>.SuccessResult(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agent {AgentId}", id);
            return StatusCode(500, ApiResponse<AgentResponse>.ErrorResult("Failed to retrieve agent"));
        }
    }

    /// <summary>
    /// Get available agents (idle status)
    /// </summary>
    [HttpGet("available")]
    public async Task<ActionResult<ApiResponse<List<AgentResponse>>>> GetAvailableAgents()
    {
        try
        {
            var agents = await _agentService.GetAvailableAgentsAsync();
            var responses = agents.Select(AgentResponse.FromAgent).ToList();
            
            return Ok(ApiResponse<List<AgentResponse>>.SuccessResult(responses));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available agents");
            return StatusCode(500, ApiResponse<List<AgentResponse>>.ErrorResult("Failed to retrieve available agents"));
        }
    }

    /// <summary>
    /// Update agent status
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateAgentStatus(Guid id, [FromBody] UpdateAgentStatusRequest request)
    {
        try
        {
            var success = await _agentService.UpdateAgentStatusAsync(id, request.Status, request.CurrentJobId, request.LastError);
            if (!success)
                return NotFound(ApiResponse<bool>.ErrorResult($"Agent {id} not found"));

            return Ok(ApiResponse<bool>.SuccessResult(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update agent {AgentId} status", id);
            return StatusCode(500, ApiResponse<bool>.ErrorResult("Failed to update agent status"));
        }
    }

    /// <summary>
    /// Send heartbeat for agent
    /// </summary>
    [HttpPost("{id:guid}/heartbeat")]
    public async Task<ActionResult<ApiResponse<bool>>> SendHeartbeat(Guid id)
    {
        try
        {
            var success = await _agentService.UpdateAgentHeartbeatAsync(id);
            if (!success)
                return NotFound(ApiResponse<bool>.ErrorResult($"Agent {id} not found"));

            return Ok(ApiResponse<bool>.SuccessResult(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update heartbeat for agent {AgentId}", id);
            return StatusCode(500, ApiResponse<bool>.ErrorResult("Failed to update heartbeat"));
        }
    }

    /// <summary>
    /// Delete agent
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteAgent(Guid id)
    {
        try
        {
            var success = await _agentService.DeleteAgentAsync(id);
            if (!success)
                return NotFound(ApiResponse<bool>.ErrorResult($"Agent {id} not found"));

            return Ok(ApiResponse<bool>.SuccessResult(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete agent {AgentId}", id);
            return StatusCode(500, ApiResponse<bool>.ErrorResult("Failed to delete agent"));
        }
    }

    /// <summary>
    /// Release agent from current job
    /// </summary>
    [HttpPost("{id:guid}/release")]
    public async Task<ActionResult<ApiResponse<bool>>> ReleaseAgent(Guid id)
    {
        try
        {
            var success = await _agentService.ReleaseAgentAsync(id);
            if (!success)
                return NotFound(ApiResponse<bool>.ErrorResult($"Agent {id} not found"));

            return Ok(ApiResponse<bool>.SuccessResult(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release agent {AgentId}", id);
            return StatusCode(500, ApiResponse<bool>.ErrorResult("Failed to release agent"));
        }
    }
}