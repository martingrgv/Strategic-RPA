namespace RPA.API.Services;

public interface ISessionService
{
    Task<string> CreateSessionAsync(string windowsUser);
    Task<bool> TerminateSessionAsync(string sessionId);
    Task<List<string>> GetActiveSessionsAsync();
    Task<bool> IsSessionActiveAsync(string sessionId);
}

public class SessionService : ISessionService
{
    private readonly ILogger<SessionService> _logger;
    private static readonly Dictionary<string, SessionInfo> _activeSessions = new();
    private readonly SemaphoreSlim _sessionsSemaphore = new(1, 1);

    public SessionService(ILogger<SessionService> logger)
    {
        _logger = logger;
    }

    public async Task<string> CreateSessionAsync(string windowsUser)
    {
        await _sessionsSemaphore.WaitAsync();
        try
        {
            var sessionId = $"RDP-Session-{Guid.NewGuid().ToString()[..8]}";
            
            // In real implementation, this would create an actual RDP session
            // For now, we'll simulate it
            var sessionInfo = new SessionInfo
            {
                SessionId = sessionId,
                WindowsUser = windowsUser,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _activeSessions[sessionId] = sessionInfo;
            
            _logger.LogInformation("Created RDP session {SessionId} for user {WindowsUser}",
                sessionId, windowsUser);

            // Simulate session creation time
            await Task.Delay(2000);
            
            return sessionId;
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
            if (!_activeSessions.TryGetValue(sessionId, out var sessionInfo))
                return false;

            sessionInfo.IsActive = false;
            sessionInfo.TerminatedAt = DateTime.UtcNow;
            
            _activeSessions.Remove(sessionId);
            
            _logger.LogInformation("Terminated RDP session {SessionId} for user {WindowsUser}",
                sessionId, sessionInfo.WindowsUser);

            // Simulate session termination time
            await Task.Delay(1000);
            
            return true;
        }
        finally
        {
            _sessionsSemaphore.Release();
        }
    }

    public async Task<List<string>> GetActiveSessionsAsync()
    {
        await Task.CompletedTask;
        return _activeSessions.Values
            .Where(s => s.IsActive)
            .Select(s => s.SessionId)
            .ToList();
    }

    public async Task<bool> IsSessionActiveAsync(string sessionId)
    {
        await Task.CompletedTask;
        return _activeSessions.TryGetValue(sessionId, out var session) && session.IsActive;
    }

    private class SessionInfo
    {
        public required string SessionId { get; set; }
        public required string WindowsUser { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? TerminatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}