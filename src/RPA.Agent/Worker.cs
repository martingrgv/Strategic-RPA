using RPA.Agent.Services;

namespace RPA.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IJobExecutor _jobExecutor;
    private readonly IConfiguration _configuration;

    public Worker(ILogger<Worker> logger, IJobExecutor jobExecutor, IConfiguration configuration)
    {
        _logger = logger;
        _jobExecutor = jobExecutor;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RPA Agent starting...");

        // Register with orchestrator
        await RegisterWithOrchestratorAsync();

        // Start heartbeat and job polling
        var tasks = new[]
        {
            SendHeartbeatAsync(stoppingToken),
            PollForJobsAsync(stoppingToken),
            RunDemoCalculatorJobAsync(stoppingToken) // Demo job for testing
        };

        try
        {
            await Task.WhenAny(tasks);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("RPA Agent shutdown requested");
        }

        _logger.LogInformation("RPA Agent stopped");
    }

    private async Task RegisterWithOrchestratorAsync()
    {
        try
        {
            var agentName = _configuration.GetValue<string>("Agent:Name", Environment.MachineName);
            _logger.LogInformation("Registering agent '{AgentName}' with orchestrator...", agentName);
            
            // In real implementation, would make HTTP call to orchestrator
            await Task.Delay(1000);
            
            _logger.LogInformation("Agent registered successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register with orchestrator");
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        var heartbeatInterval = TimeSpan.FromSeconds(
            _configuration.GetValue<int>("Agent:HeartbeatIntervalSeconds", 30));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // In real implementation, would send HTTP heartbeat to orchestrator
                _logger.LogDebug("Sending heartbeat to orchestrator");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send heartbeat");
            }

            await Task.Delay(heartbeatInterval, cancellationToken);
        }
    }

    private async Task PollForJobsAsync(CancellationToken cancellationToken)
    {
        var pollInterval = TimeSpan.FromSeconds(
            _configuration.GetValue<int>("Agent:JobPollIntervalSeconds", 5));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // In real implementation, would poll orchestrator for jobs via HTTP
                _logger.LogDebug("Polling for jobs from orchestrator");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to poll for jobs");
            }

            await Task.Delay(pollInterval, cancellationToken);
        }
    }

    private async Task RunDemoCalculatorJobAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(5000, cancellationToken); // Wait for startup

        try
        {
            _logger.LogInformation("Running demo calculator automation...");
            
            var calculatorJob = CalculatorAutomation.CreateSimpleCalculation();
            var result = await _jobExecutor.ExecuteJobAsync(calculatorJob);
            
            if (result.Success)
            {
                _logger.LogInformation("Demo calculator automation completed successfully in {Duration}ms", 
                    result.ExecutionTime.TotalMilliseconds);
            }
            else
            {
                _logger.LogError("Demo calculator automation failed: {ErrorMessage}", result.ErrorDetails);
            }

            // Run complex calculation after 10 seconds
            await Task.Delay(10000, cancellationToken);
            
            var complexJob = CalculatorAutomation.CreateComplexCalculation();
            var complexResult = await _jobExecutor.ExecuteJobAsync(complexJob);
            
            _logger.LogInformation("Complex calculator automation result: {Success}", complexResult.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running demo calculator job");
        }
    }
}
