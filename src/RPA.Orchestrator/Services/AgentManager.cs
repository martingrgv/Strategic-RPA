using RPA.Orchestrator.Models;
using System.Diagnostics;

namespace RPA.Orchestrator.Services;

public interface IAgentManager
{
    Task<Agent> RegisterAgentAsync(string name, string windowsUser, AgentCapabilities? capabilities = null);
    Task<bool> UnregisterAgentAsync(Guid agentId);
    Task<Agent?> GetAgentAsync(Guid agentId);
    Task<List<Agent>> GetAllAgentsAsync();
    Task<List<Agent>> GetAvailableAgentsAsync();
    Task<bool> UpdateAgentStatusAsync(Guid agentId, AgentStatus status, string? error = null);
    Task<bool> UpdateAgentHeartbeatAsync(Guid agentId);
    Task<Agent?> AssignJobToAgentAsync(AutomationJob job);
    Task<bool> ReleaseAgentAsync(Guid agentId);
    Task<List<Agent>> GetOfflineAgentsAsync(TimeSpan heartbeatTimeout);
    Task<bool> RecycleAgentAsync(Guid agentId);
}

public class AgentManager : IAgentManager
{
    private readonly ILogger<AgentManager> _logger;
    private readonly ISessionManager _sessionManager;
    private static readonly Dictionary<Guid, Agent> _agents = new();
    private readonly SemaphoreSlim _agentsSemaphore = new(1, 1);

    public AgentManager(ILogger<AgentManager> logger, ISessionManager sessionManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
    }

    public async Task<Agent> RegisterAgentAsync(string name, string windowsUser, AgentCapabilities? capabilities = null)
    {
        await _agentsSemaphore.WaitAsync();
        try
        {
            // Create RDP session for the agent
            var session = await _sessionManager.CreateSessionAsync(windowsUser);
            
            var agent = new Agent
            {
                Name = name,
                WindowsUser = windowsUser,
                SessionId = session.SessionId,
                Status = AgentStatus.Starting,
                Capabilities = capabilities ?? new AgentCapabilities(),
                EndpointUrl = $"http://localhost:{session.Port}/agent"
            };

            _agents[agent.Id] = agent;
            
            // Assign session to agent
            await _sessionManager.AssignSessionToAgentAsync(session.SessionId, agent.Id.ToString());

            _logger.LogInformation("Registered agent {AgentId} '{AgentName}' with session {SessionId}", 
                agent.Id, agent.Name, session.SessionId);

            // Simulate agent startup
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000); // Simulate startup time
                await UpdateAgentStatusAsync(agent.Id, AgentStatus.Idle);
                _logger.LogInformation("Agent {AgentId} '{AgentName}' is now ready", agent.Id, agent.Name);
            });

            return agent;
        }
        finally
        {
            _agentsSemaphore.Release();
        }
    }

    public async Task<bool> UnregisterAgentAsync(Guid agentId)
    {
        await _agentsSemaphore.WaitAsync();
        try
        {
            if (!_agents.TryGetValue(agentId, out var agent))
                return false;

            // Terminate the agent's session
            await _sessionManager.TerminateSessionAsync(agent.SessionId);
            
            _agents.Remove(agentId);
            
            _logger.LogInformation("Unregistered agent {AgentId} '{AgentName}' and terminated session {SessionId}", 
                agentId, agent.Name, agent.SessionId);
            
            return true;
        }
        finally
        {
            _agentsSemaphore.Release();
        }
    }

    public async Task<Agent?> GetAgentAsync(Guid agentId)
    {
        await Task.CompletedTask;
        _agents.TryGetValue(agentId, out var agent);
        return agent;
    }

    public async Task<List<Agent>> GetAllAgentsAsync()
    {
        await Task.CompletedTask;
        return _agents.Values.ToList();
    }

    public async Task<List<Agent>> GetAvailableAgentsAsync()
    {
        await Task.CompletedTask;
        return _agents.Values
            .Where(a => a.Status == AgentStatus.Idle)
            .ToList();
    }

    public async Task<bool> UpdateAgentStatusAsync(Guid agentId, AgentStatus status, string? error = null)
    {
        await _agentsSemaphore.WaitAsync();
        try
        {
            if (!_agents.TryGetValue(agentId, out var agent))
                return false;

            var oldStatus = agent.Status;
            agent.Status = status;
            agent.LastHeartbeat = DateTime.UtcNow;
            
            if (!string.IsNullOrEmpty(error))
                agent.LastError = error;
            else if (status != AgentStatus.Error)
                agent.LastError = null;

            _logger.LogDebug("Agent {AgentId} status changed from {OldStatus} to {NewStatus}", 
                agentId, oldStatus, status);
            
            return true;
        }
        finally
        {
            _agentsSemaphore.Release();
        }
    }

    public async Task<bool> UpdateAgentHeartbeatAsync(Guid agentId)
    {
        await _agentsSemaphore.WaitAsync();
        try
        {
            if (!_agents.TryGetValue(agentId, out var agent))
                return false;

            agent.LastHeartbeat = DateTime.UtcNow;
            
            // If agent was offline, mark it as idle
            if (agent.Status == AgentStatus.Offline)
                agent.Status = AgentStatus.Idle;
            
            return true;
        }
        finally
        {
            _agentsSemaphore.Release();
        }
    }

    public async Task<Agent?> AssignJobToAgentAsync(AutomationJob job)
    {
        var availableAgents = await GetAvailableAgentsAsync();
        
        // Find best suited agent based on capabilities
        var suitableAgent = availableAgents
            .Where(a => CanHandleJob(a, job))
            .OrderByDescending(a => a.Metrics.SuccessRate)
            .ThenBy(a => a.JobsExecuted)
            .FirstOrDefault();

        if (suitableAgent != null)
        {
            await UpdateAgentStatusAsync(suitableAgent.Id, AgentStatus.Busy);
            suitableAgent.CurrentJobId = job.Id.ToString();
            
            _logger.LogInformation("Assigned job {JobId} to agent {AgentId} '{AgentName}'", 
                job.Id, suitableAgent.Id, suitableAgent.Name);
        }

        return suitableAgent;
    }

    public async Task<bool> ReleaseAgentAsync(Guid agentId)
    {
        await _agentsSemaphore.WaitAsync();
        try
        {
            if (!_agents.TryGetValue(agentId, out var agent))
                return false;

            agent.Status = AgentStatus.Idle;
            agent.CurrentJobId = null;
            agent.JobsExecuted++;
            agent.LastHeartbeat = DateTime.UtcNow;

            // Update metrics
            agent.Metrics.TotalJobsCompleted++;
            agent.Metrics.LastJobCompletedAt = DateTime.UtcNow;

            // Release session
            await _sessionManager.ReleaseSessionAsync(agent.SessionId);

            // Check if agent needs recycling (50 jobs as per architecture)
            if (agent.JobsExecuted >= 50)
            {
                _logger.LogInformation("Agent {AgentId} has executed {JobCount} jobs, scheduling for recycling", 
                    agentId, agent.JobsExecuted);
                
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000); // Brief delay
                    await RecycleAgentAsync(agentId);
                });
            }

            return true;
        }
        finally
        {
            _agentsSemaphore.Release();
        }
    }

    public async Task<List<Agent>> GetOfflineAgentsAsync(TimeSpan heartbeatTimeout)
    {
        await Task.CompletedTask;
        var cutoffTime = DateTime.UtcNow - heartbeatTimeout;
        
        return _agents.Values
            .Where(a => a.LastHeartbeat.HasValue && 
                       a.LastHeartbeat.Value < cutoffTime && 
                       a.Status != AgentStatus.Offline)
            .ToList();
    }

    public async Task<bool> RecycleAgentAsync(Guid agentId)
    {
        await _agentsSemaphore.WaitAsync();
        try
        {
            if (!_agents.TryGetValue(agentId, out var agent))
                return false;

            _logger.LogInformation("Recycling agent {AgentId} '{AgentName}'", agentId, agent.Name);
            
            agent.Status = AgentStatus.Recycling;
            
            // Recycle the session
            await _sessionManager.RecycleSessionAsync(agent.SessionId);
            
            // Reset agent state
            agent.JobsExecuted = 0;
            agent.LastError = null;
            agent.Metrics = new AgentMetrics();

            // Simulate restart time
            await Task.Delay(5000);
            
            agent.Status = AgentStatus.Idle;
            agent.LastHeartbeat = DateTime.UtcNow;
            
            _logger.LogInformation("Agent {AgentId} '{AgentName}' recycled successfully", agentId, agent.Name);
            
            return true;
        }
        finally
        {
            _agentsSemaphore.Release();
        }
    }

    private bool CanHandleJob(Agent agent, AutomationJob job)
    {
        // Check if agent supports the application
        if (agent.Capabilities.SupportedApplications.Count > 0)
        {
            return agent.Capabilities.SupportedApplications.Any(app => 
                job.ApplicationPath.Contains(app, StringComparison.OrdinalIgnoreCase));
        }
        
        // Generic agent can handle any job
        return true;
    }
}