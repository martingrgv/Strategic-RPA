using RPA.Orchestrator.Models;
using System.Text;
using System.Text.Json;

namespace RPA.Orchestrator.Services;

public interface IAgentCommunicationService
{
    Task<bool> SendJobToAgentAsync(Agent agent, AutomationJob job);
    Task<bool> CancelJobOnAgentAsync(Agent agent, Guid jobId);
    Task<AgentStatus> GetAgentStatusAsync(Agent agent);
}

public class AgentCommunicationService : IAgentCommunicationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AgentCommunicationService> _logger;

    public AgentCommunicationService(HttpClient httpClient, ILogger<AgentCommunicationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> SendJobToAgentAsync(Agent agent, AutomationJob job)
    {
        try
        {
            // Send job to agent via HTTP API
            var agentEndpoint = $"{agent.EndpointUrl}/jobs";
            var json = JsonSerializer.Serialize(job);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(agentEndpoint, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully sent job {JobId} to agent {AgentId}", 
                    job.Id, agent.Id);
                return true;
            }

            _logger.LogWarning("Failed to send job {JobId} to agent {AgentId}. Status: {StatusCode}", 
                job.Id, agent.Id, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending job {JobId} to agent {AgentId}", job.Id, agent.Id);
            return false;
        }
    }

    public async Task<bool> CancelJobOnAgentAsync(Agent agent, Guid jobId)
    {
        try
        {
            var cancelEndpoint = $"{agent.EndpointUrl}/jobs/{jobId}/cancel";
            var response = await _httpClient.PostAsync(cancelEndpoint, null);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling job {JobId} on agent {AgentId}", jobId, agent.Id);
            return false;
        }
    }

    public async Task<AgentStatus> GetAgentStatusAsync(Agent agent)
    {
        try
        {
            var statusEndpoint = $"{agent.EndpointUrl}/status";
            var response = await _httpClient.GetAsync(statusEndpoint);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var statusData = JsonSerializer.Deserialize<AgentStatusResponse>(jsonString);
                return statusData?.Status ?? AgentStatus.Error;
            }
            
            return AgentStatus.Error;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status from agent {AgentId}", agent.Id);
            return AgentStatus.Error;
        }
    }
}

public class AgentStatusResponse
{
    public AgentStatus Status { get; set; }
    public string? CurrentJobId { get; set; }
    public DateTime LastUpdate { get; set; }
}