using RPA.Orchestrator.Models;

namespace RPA.Orchestrator.Services;

public interface ISessionManager
{
    Task<RdpSession> CreateSessionAsync(string windowsUser);
    Task<bool> TerminateSessionAsync(string sessionId);
    Task<RdpSession?> GetSessionAsync(string sessionId);
    Task<List<RdpSession>> GetActiveSessionsAsync();
    Task<bool> AssignSessionToAgentAsync(string sessionId, string agentId);
    Task<bool> ReleaseSessionAsync(string sessionId);
    Task<bool> RecycleSessionAsync(string sessionId);
    Task<bool> CheckSessionHealthAsync(string sessionId);
}

public class SessionManager : ISessionManager
{
    private readonly ILogger<SessionManager> _logger;
    private readonly IConfiguration _configuration;
    private static readonly Dictionary<string, RdpSession> _sessions = new();
    private readonly SemaphoreSlim _sessionsSemaphore = new(1, 1);
    private readonly Random _random = new();

    public SessionManager(ILogger<SessionManager> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<RdpSession> CreateSessionAsync(string windowsUser)
    {
        await _sessionsSemaphore.WaitAsync();
        try
        {
            var sessionId = Guid.NewGuid().ToString();
            var basePort = _configuration.GetValue<int>("RDP:BasePort", 3390);
            var port = basePort + _random.Next(1000); // Dynamic port allocation

            var session = new RdpSession
            {
                SessionId = sessionId,
                WindowsUser = windowsUser,
                Port = port,
                Status = SessionStatus.Starting,
                CreatedAt = DateTime.UtcNow
            };

            // Simulate RDP session creation
            await SimulateCreateRdpSessionAsync(session);

            _sessions[sessionId] = session;

            _logger.LogInformation("Created RDP session {SessionId} for user '{WindowsUser}' on port {Port}",
                sessionId, windowsUser, port);

            return session;
        }
        finally
        {
            _sessionsSemaphore.Release();
        }
    }

    public async Task<bool> TerminateSessionAsync(string sessionId)
    {
        await _sessionsSemaphore.WaitAsync();
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return false;

            session.Status = SessionStatus.Terminating;

            // Simulate RDP session termination
            await SimulateTerminateRdpSessionAsync(session);

            _sessions.Remove(sessionId);

            _logger.LogInformation("Terminated RDP session {SessionId} for user '{WindowsUser}'",
                sessionId, session.WindowsUser);

            return true;
        }
        finally
        {
            _sessionsSemaphore.Release();
        }
    }

    public async Task<RdpSession?> GetSessionAsync(string sessionId)
    {
        await Task.CompletedTask;
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    public async Task<List<RdpSession>> GetActiveSessionsAsync()
    {
        await Task.CompletedTask;
        return _sessions.Values
            .Where(s => s.Status == SessionStatus.Active || s.Status == SessionStatus.Busy)
            .ToList();
    }

    public async Task<bool> AssignSessionToAgentAsync(string sessionId, string agentId)
    {
        await _sessionsSemaphore.WaitAsync();
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return false;

            session.AssignedAgentId = agentId;
            session.Status = SessionStatus.Active;
            session.LastActivity = DateTime.UtcNow;

            _logger.LogDebug("Assigned session {SessionId} to agent {AgentId}",
                sessionId, agentId);

            return true;
        }
        finally
        {
            _sessionsSemaphore.Release();
        }
    }

    public async Task<bool> ReleaseSessionAsync(string sessionId)
    {
        await _sessionsSemaphore.WaitAsync();
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return false;

            session.Status = SessionStatus.Active;
            session.LastActivity = DateTime.UtcNow;
            session.JobsProcessed++;

            _logger.LogDebug("Released session {SessionId} from agent {AgentId}",
                sessionId, session.AssignedAgentId);

            return true;
        }
        finally
        {
            _sessionsSemaphore.Release();
        }
    }

    public async Task<bool> RecycleSessionAsync(string sessionId)
    {
        await _sessionsSemaphore.WaitAsync();
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return false;

            _logger.LogInformation("Recycling session {SessionId} for user '{WindowsUser}'",
                sessionId, session.WindowsUser);

            var oldSession = session;
            oldSession.Status = SessionStatus.Recycling;

            // Terminate old session
            await SimulateTerminateRdpSessionAsync(oldSession);

            // Create new session with same user
            var newSession = await CreateNewSessionForUserAsync(oldSession.WindowsUser);
            
            // Update session mapping
            _sessions[sessionId] = newSession;
            newSession.SessionId = sessionId; // Keep same session ID for agent consistency

            _logger.LogInformation("Session {SessionId} recycled successfully",
                sessionId);

            return true;
        }
        finally
        {
            _sessionsSemaphore.Release();
        }
    }

    public async Task<bool> CheckSessionHealthAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return false;

        // Simulate health check by pinging the session
        await Task.Delay(100);

        var isHealthy = _random.NextDouble() > 0.05; // 95% health check success rate

        if (!isHealthy)
        {
            _logger.LogWarning("Health check failed for session {SessionId}", sessionId);
            session.Status = SessionStatus.Unhealthy;
        }
        else if (session.Status == SessionStatus.Unhealthy)
        {
            session.Status = SessionStatus.Active;
            _logger.LogInformation("Session {SessionId} health restored", sessionId);
        }

        session.LastHealthCheck = DateTime.UtcNow;
        return isHealthy;
    }

    private async Task SimulateCreateRdpSessionAsync(RdpSession session)
    {
        // Simulate time to create RDP session
        await Task.Delay(2000);

        // In real implementation, this would:
        // 1. Create Windows user if not exists
        // 2. Start RDP service on specified port
        // 3. Configure session isolation
        // 4. Set up monitoring

        session.Status = SessionStatus.Active;
        session.LastActivity = DateTime.UtcNow;

        _logger.LogDebug("RDP session {SessionId} created successfully", session.SessionId);
    }

    private async Task SimulateTerminateRdpSessionAsync(RdpSession session)
    {
        // Simulate time to terminate RDP session
        await Task.Delay(1000);

        // In real implementation, this would:
        // 1. Gracefully close any open applications
        // 2. Log off the user
        // 3. Clean up session resources
        // 4. Stop RDP service on the port

        session.Status = SessionStatus.Terminated;
        session.TerminatedAt = DateTime.UtcNow;

        _logger.LogDebug("RDP session {SessionId} terminated successfully", session.SessionId);
    }

    private async Task<RdpSession> CreateNewSessionForUserAsync(string windowsUser)
    {
        var sessionId = Guid.NewGuid().ToString();
        var basePort = _configuration.GetValue<int>("RDP:BasePort", 3390);
        var port = basePort + _random.Next(1000);

        var session = new RdpSession
        {
            SessionId = sessionId,
            WindowsUser = windowsUser,
            Port = port,
            Status = SessionStatus.Starting,
            CreatedAt = DateTime.UtcNow
        };

        await SimulateCreateRdpSessionAsync(session);

        _logger.LogDebug("Created new session for user '{WindowsUser}' on port {Port}",
            windowsUser, port);

        return session;
    }
}