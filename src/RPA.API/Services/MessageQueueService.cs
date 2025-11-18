using RPA.API.Models;
using System.Text;
using System.Text.Json;

namespace RPA.API.Services;

public interface IMessageQueueService
{
    Task PublishJobAsync(AutomationJob job);
    Task PublishJobCancellationAsync(Guid jobId);
}

public class MessageQueueService : IMessageQueueService
{
    private readonly ILogger<MessageQueueService> _logger;
    private readonly IConfiguration _configuration;

    public MessageQueueService(ILogger<MessageQueueService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task PublishJobAsync(AutomationJob job)
    {
        try
        {
            // For now, simulate AMQP by directly calling orchestrator HTTP API
            // In production, you'd use RabbitMQ, Azure Service Bus, etc.
            
            var orchestratorUrl = _configuration.GetValue<string>("Orchestrator:BaseUrl", "http://localhost:5001");
            using var httpClient = new HttpClient();
            
            var message = new
            {
                Type = "JobCreated",
                JobId = job.Id,
                Job = job,
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(message);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var endpoint = $"{orchestratorUrl}/api/orchestrator/jobs";
            var response = await httpClient.PostAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully published job {JobId} to orchestrator", job.Id);
            }
            else
            {
                _logger.LogError("Failed to publish job {JobId} to orchestrator. Status: {StatusCode}", 
                    job.Id, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing job {JobId} to message queue", job.Id);
            throw;
        }
    }

    public async Task PublishJobCancellationAsync(Guid jobId)
    {
        try
        {
            var orchestratorUrl = _configuration.GetValue<string>("Orchestrator:BaseUrl", "http://localhost:5001");
            using var httpClient = new HttpClient();
            
            var message = new
            {
                Type = "JobCancelled",
                JobId = jobId,
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(message);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var endpoint = $"{orchestratorUrl}/api/orchestrator/jobs/{jobId}/cancel";
            var response = await httpClient.PostAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully published job cancellation {JobId} to orchestrator", jobId);
            }
            else
            {
                _logger.LogError("Failed to publish job cancellation {JobId} to orchestrator. Status: {StatusCode}", 
                    jobId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing job cancellation {JobId} to message queue", jobId);
            throw;
        }
    }
}

// Real AMQP implementation would look like this:
// public class RabbitMqMessageQueueService : IMessageQueueService
// {
//     private readonly IModel _channel;
//     
//     public async Task PublishJobAsync(AutomationJob job)
//     {
//         var message = JsonSerializer.SerializeToUtf8Bytes(job);
//         _channel.BasicPublish(
//             exchange: "rpa.jobs",
//             routingKey: "job.created",
//             body: message);
//     }
// }