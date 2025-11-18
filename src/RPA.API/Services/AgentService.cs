using RPA.API.Models;

namespace RPA.API.Services;

public interface IAgentService
{
    Task<Agent> CreateAgentAsync(string name, string windowsUser, AgentCapabilities? capabilities = null);
    Task<Agent?> GetAgentAsync(Guid agentId);
    Task<List<Agent>> GetAllAgentsAsync();
    Task<List<Agent>> GetAvailableAgentsAsync();
    Task<bool> UpdateAgentStatusAsync(Guid agentId, AgentStatus status, string? currentJobId = null, string? lastError = null);
    Task<bool> UpdateAgentHeartbeatAsync(Guid agentId);
    Task<bool> DeleteAgentAsync(Guid agentId);
    Task<Agent?> AssignJobToAgentAsync(Guid jobId, string applicationPath);
    Task<bool> ReleaseAgentAsync(Guid agentId);
}

public class AgentService : IAgentService
{
    private readonly ILogger<AgentService> _logger;
    private readonly ISessionService _sessionService;
    private static readonly Dictionary<Guid, Agent> _agents = new();
    private readonly SemaphoreSlim _agentsSemaphore = new(1, 1);

    public AgentService(ILogger<AgentService> logger, ISessionService sessionService)
    {
        _logger = logger;
        _sessionService = sessionService;
    }

    public async Task<Agent> CreateAgentAsync(string name, string windowsUser, AgentCapabilities? capabilities = null)
    {
        await _agentsSemaphore.WaitAsync();
        try
        {
            var sessionId = await _sessionService.CreateSessionAsync(windowsUser);
            
            var agent = new Agent
            {
                Name = name,
                WindowsUser = windowsUser,
                SessionId = sessionId,
                Status = AgentStatus.Starting,
                Capabilities = capabilities ?? new AgentCapabilities()
            };

            _agents[agent.Id] = agent;
            
            _logger.LogInformation("Created agent {AgentId} with session {SessionId} for user {WindowsUser}",
                agent.Id, sessionId, windowsUser);

            // Simulate agent startup process
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000); // Simulate startup time
                await UpdateAgentStatusAsync(agent.Id, AgentStatus.Idle);
                _logger.LogInformation("Agent {AgentId} is now ready", agent.Id);
            });

            return agent;
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

    public async Task<bool> UpdateAgentStatusAsync(Guid agentId, AgentStatus status, string? currentJobId = null, string? lastError = null)
    {
        await _agentsSemaphore.WaitAsync();
        try
        {
            if (!_agents.TryGetValue(agentId, out var agent))
                return false;

            agent.Status = status;
            agent.LastHeartbeat = DateTime.UtcNow;
            
            if (currentJobId != null)
                agent.CurrentJobId = currentJobId;
            
            if (lastError != null)
                agent.LastError = lastError;

            _logger.LogDebug("Updated agent {AgentId} status to {Status}", agentId, status);
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
            return true;
        }
        finally
        {
            _agentsSemaphore.Release();
        }
    }

    public async Task<bool> DeleteAgentAsync(Guid agentId)
    {
        await _agentsSemaphore.WaitAsync();
        try
        {
            if (!_agents.TryGetValue(agentId, out var agent))
                return false;

            // Terminate session first
            await _sessionService.TerminateSessionAsync(agent.SessionId);
            
            _agents.Remove(agentId);
            
            _logger.LogInformation("Deleted agent {AgentId} and terminated session {SessionId}",
                agentId, agent.SessionId);
            
            return true;
        }
        finally
        {
            _agentsSemaphore.Release();
        }
    }

    public async Task<Agent?> AssignJobToAgentAsync(Guid jobId, string applicationPath)
    {
        var availableAgents = await GetAvailableAgentsAsync();
        
        // Find agent that supports the application
        var suitableAgent = availableAgents.FirstOrDefault(a => 
            a.Capabilities.SupportedApplications.Count == 0 || // Generic agent
            a.Capabilities.SupportedApplications.Any(app => 
                applicationPath.Contains(app, StringComparison.OrdinalIgnoreCase)));

        if (suitableAgent != null)
        {
            await UpdateAgentStatusAsync(suitableAgent.Id, AgentStatus.Busy, jobId.ToString());
            _logger.LogInformation("Assigned job {JobId} to agent {AgentId}", jobId, suitableAgent.Id);
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

            // Check if agent needs to be recycled (after 50 jobs as per architecture)
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

    private async Task RecycleAgentAsync(Guid agentId)
    {
        await _agentsSemaphore.WaitAsync();
        try
        {
            if (!_agents.TryGetValue(agentId, out var agent))
                return;

            _logger.LogInformation("Recycling agent {AgentId}", agentId);
            
            // Terminate old session
            await _sessionService.TerminateSessionAsync(agent.SessionId);
            
            // Create new session
            var newSessionId = await _sessionService.CreateSessionAsync(agent.WindowsUser);
            
            agent.SessionId = newSessionId;
            agent.Status = AgentStatus.Starting;
            agent.JobsExecuted = 0;
            agent.LastError = null;

            // Simulate restart time
            await Task.Delay(5000);
            await UpdateAgentStatusAsync(agentId, AgentStatus.Idle);
            
            _logger.LogInformation("Agent {AgentId} recycled with new session {SessionId}",
                agentId, newSessionId);
        }
        finally
        {
            _agentsSemaphore.Release();
        }
    }
}