using RPA.Agent.Models;
using System.Text;
using System.Text.Json;

namespace RPA.Agent.Services;

public interface IApiReportingService
{
    Task ReportJobStatusAsync(Guid jobId, JobStatus status, string? result = null, string? errorMessage = null);
    Task SendHeartbeatAsync(string agentId);
}

public class ApiReportingService : IApiReportingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiReportingService> _logger;
    private readonly IConfiguration _configuration;

    public ApiReportingService(HttpClient httpClient, ILogger<ApiReportingService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task ReportJobStatusAsync(Guid jobId, JobStatus status, string? result = null, string? errorMessage = null)
    {
        try
        {
            var apiBaseUrl = _configuration.GetValue<string>("Api:BaseUrl", "http://localhost:5000");
            var endpoint = $"{apiBaseUrl}/api/automation/jobs/{jobId}/status";

            var statusUpdate = new
            {
                Status = status.ToString(),
                Result = result,
                ErrorMessage = errorMessage,
                CompletedAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(statusUpdate);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PatchAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully reported job {JobId} status {Status} to API", jobId, status);
            }
            else
            {
                _logger.LogWarning("Failed to report job {JobId} status to API. Response: {StatusCode}", 
                    jobId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting job {JobId} status to API", jobId);
        }
    }

    public async Task SendHeartbeatAsync(string agentId)
    {
        try
        {
            var apiBaseUrl = _configuration.GetValue<string>("Api:BaseUrl", "http://localhost:5000");
            var endpoint = $"{apiBaseUrl}/api/agents/{agentId}/heartbeat";

            var response = await _httpClient.PostAsync(endpoint, null);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Successfully sent heartbeat for agent {AgentId}", agentId);
            }
            else
            {
                _logger.LogWarning("Failed to send heartbeat for agent {AgentId}. Response: {StatusCode}", 
                    agentId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending heartbeat for agent {AgentId}", agentId);
        }
    }
}