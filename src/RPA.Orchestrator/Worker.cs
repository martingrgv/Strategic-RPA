using RPA.Orchestrator.Services;

namespace RPA.Orchestrator;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IJobScheduler _jobScheduler;
    private readonly IAgentManager _agentManager;
    private readonly IHealthMonitor _healthMonitor;
    private readonly IConfiguration _configuration;

    public Worker(
        ILogger<Worker> logger, 
        IJobScheduler jobScheduler,
        IAgentManager agentManager,
        IHealthMonitor healthMonitor,
        IConfiguration configuration)
    {
        _logger = logger;
        _jobScheduler = jobScheduler;
        _agentManager = agentManager;
        _healthMonitor = healthMonitor;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Strategic RPA Orchestrator starting...");

        // Initialize default agents if configured
        await InitializeDefaultAgentsAsync();

        // Start orchestrator loops
        var orchestrationTasks = new[]
        {
            ProcessJobQueueAsync(stoppingToken),
            MonitorAgentHealthAsync(stoppingToken),
            PerformMaintenanceAsync(stoppingToken)
        };

        try
        {
            await Task.WhenAll(orchestrationTasks);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Orchestrator shutdown requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in orchestrator");
        }

        _logger.LogInformation("Strategic RPA Orchestrator stopped");
    }

    private async Task ProcessJobQueueAsync(CancellationToken cancellationToken)
    {
        var processInterval = TimeSpan.FromSeconds(
            _configuration.GetValue<int>("Orchestrator:JobProcessingIntervalSeconds", 5));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Processing job queue...");
                await _jobScheduler.ProcessJobQueueAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job queue");
            }

            await Task.Delay(processInterval, cancellationToken);
        }
    }

    private async Task MonitorAgentHealthAsync(CancellationToken cancellationToken)
    {
        var healthCheckInterval = TimeSpan.FromMinutes(
            _configuration.GetValue<int>("Orchestrator:HealthCheckIntervalMinutes", 2));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Performing health monitoring...");
                await _healthMonitor.PerformHealthChecksAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health monitoring");
            }

            await Task.Delay(healthCheckInterval, cancellationToken);
        }
    }

    private async Task PerformMaintenanceAsync(CancellationToken cancellationToken)
    {
        var maintenanceInterval = TimeSpan.FromHours(
            _configuration.GetValue<int>("Orchestrator:MaintenanceIntervalHours", 4));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Performing maintenance tasks...");
                await _healthMonitor.CleanupResourcesAsync();
                await LogOrchestratorMetricsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during maintenance");
            }

            await Task.Delay(maintenanceInterval, cancellationToken);
        }
    }

    private async Task InitializeDefaultAgentsAsync()
    {
        var defaultAgentCount = _configuration.GetValue<int>("Orchestrator:DefaultAgentCount", 2);
        
        _logger.LogInformation("Initializing {AgentCount} default agents...", defaultAgentCount);

        var tasks = new List<Task>();
        for (int i = 1; i <= defaultAgentCount; i++)
        {
            var windowsUser = $"AutoAgent{i:D2}";
            var agentName = $"Agent-{i:D2}";
            
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var agent = await _agentManager.RegisterAgentAsync(agentName, windowsUser);
                    _logger.LogInformation("Initialized agent '{AgentName}' with ID {AgentId}", 
                        agentName, agent.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize agent '{AgentName}'", agentName);
                }
            }));
        }

        await Task.WhenAll(tasks);
        _logger.LogInformation("Agent initialization completed");
    }

    private async Task LogOrchestratorMetricsAsync()
    {
        try
        {
            var metrics = await _healthMonitor.GetMetricsAsync();
            
            _logger.LogInformation(
                "Orchestrator Metrics - Agents: {TotalAgents} (Idle: {IdleAgents}, Busy: {BusyAgents}, Offline: {OfflineAgents}), " +
                "Jobs: {PendingJobs} pending, {QueuedJobs} queued, {RunningJobs} running, " +
                "Sessions: {ActiveSessions}, Success Rate: {SuccessRate:P1}",
                metrics.TotalAgents, metrics.IdleAgents, metrics.BusyAgents, metrics.OfflineAgents,
                metrics.PendingJobs, metrics.QueuedJobs, metrics.RunningJobs,
                metrics.ActiveSessions, metrics.OverallSuccessRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging orchestrator metrics");
        }
    }
}
