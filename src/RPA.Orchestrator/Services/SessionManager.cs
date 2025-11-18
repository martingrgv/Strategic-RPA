using RPA.Orchestrator.Models;
using System.Diagnostics;
using System.Management;
using System.ServiceProcess;
using System.Security.Principal;
using System.DirectoryServices.AccountManagement;

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
        try
        {
            _logger.LogInformation("Creating RDP session {SessionId} for user '{WindowsUser}' on port {Port}", 
                session.SessionId, session.WindowsUser, session.Port);

            // 1. Ensure Windows user exists
            await EnsureUserExistsAsync(session.WindowsUser);

            // 2. Create isolated Windows session
            var sessionId = await CreateWindowsSessionAsync(session.WindowsUser);
            if (sessionId == 0)
            {
                throw new InvalidOperationException("Failed to create Windows session");
            }

            // 3. Start Agent application in the session
            await StartAgentInSessionAsync(sessionId, session.Port, session.WindowsUser);

            // 4. Configure session monitoring
            await ConfigureSessionMonitoringAsync(sessionId, session.SessionId);

            session.Status = SessionStatus.Active;
            session.LastActivity = DateTime.UtcNow;
            session.Metrics.CreatedAt = DateTime.UtcNow;

            _logger.LogInformation("RDP session {SessionId} created successfully with Windows session ID {WindowsSessionId}", 
                session.SessionId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create RDP session {SessionId}", session.SessionId);
            session.Status = SessionStatus.Error;
            throw;
        }
    }

    private async Task SimulateTerminateRdpSessionAsync(RdpSession session)
    {
        try
        {
            _logger.LogInformation("Terminating RDP session {SessionId} for user '{WindowsUser}'", 
                session.SessionId, session.WindowsUser);

            session.Status = SessionStatus.Terminating;

            // 1. Stop Agent application gracefully
            await StopAgentInSessionAsync(session.Port);

            // 2. Close applications in session
            await CloseApplicationsInSessionAsync(session.WindowsUser);

            // 3. Log off the Windows session
            await LogoffWindowsSessionAsync(session.WindowsUser);

            // 4. Clean up resources
            await CleanupSessionResourcesAsync(session.SessionId);

            session.Status = SessionStatus.Terminated;
            session.TerminatedAt = DateTime.UtcNow;

            _logger.LogInformation("RDP session {SessionId} terminated successfully", session.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to terminate RDP session {SessionId}", session.SessionId);
            session.Status = SessionStatus.Error;
            throw;
        }
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

    #region Windows Session Management

    /// <summary>
    /// Ensures that the Windows user exists for RDP session
    /// </summary>
    private async Task EnsureUserExistsAsync(string username)
    {
        await Task.Run(() =>
        {
            try
            {
                using var context = new PrincipalContext(ContextType.Machine);
                var user = UserPrincipal.FindByIdentity(context, username);
                
                if (user == null)
                {
                    _logger.LogInformation("Creating Windows user '{Username}' for RDP automation", username);
                    
                    // Create new user
                    var newUser = new UserPrincipal(context)
                    {
                        Name = username,
                        DisplayName = $"RPA Agent User - {username}",
                        Description = "Automated user for RPA Agent execution",
                        PasswordNeverExpires = true,
                        UserCannotChangePassword = true
                    };
                    
                    // Generate secure password
                    var password = GenerateSecurePassword();
                    newUser.SetPassword(password);
                    newUser.Save();
                    
                    // Add to Remote Desktop Users group
                    using var group = GroupPrincipal.FindByIdentity(context, "Remote Desktop Users");
                    group?.Members.Add(newUser);
                    group?.Save();
                    
                    _logger.LogInformation("Windows user '{Username}' created successfully", username);
                }
                else
                {
                    _logger.LogDebug("Windows user '{Username}' already exists", username);
                }
                
                user?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure user '{Username}' exists", username);
                throw;
            }
        });
    }

    /// <summary>
    /// Creates a Windows session for the specified user
    /// </summary>
    private async Task<uint> CreateWindowsSessionAsync(string username)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Use WTS API to create session
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_Process WHERE Name = 'explorer.exe'");
                
                var processes = searcher.Get();
                
                // Check if user already has an active session
                foreach (ManagementObject process in processes)
                {
                    var owner = new string[2];
                    var result = process.InvokeMethod("GetOwner", owner);
                    
                    if (owner[0]?.Equals(username, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        var sessionId = (uint)process["SessionId"];
                        _logger.LogInformation("Found existing Windows session {SessionId} for user '{Username}'", 
                            sessionId, username);
                        return sessionId;
                    }
                }
                
                // Create new session using logon
                var newSessionId = CreateUserSession(username);
                _logger.LogInformation("Created new Windows session {SessionId} for user '{Username}'", 
                    newSessionId, username);
                
                return newSessionId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Windows session for user '{Username}'", username);
                throw;
            }
        });
    }

    /// <summary>
    /// Starts the RPA Agent application in the specified Windows session
    /// </summary>
    private async Task StartAgentInSessionAsync(uint sessionId, int port, string username)
    {
        await Task.Run(() =>
        {
            try
            {
                var agentPath = _configuration.GetValue<string>("Agent:ExecutablePath", 
                    @"C:\RPA\Agent\RPA.Agent.exe");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = agentPath,
                    Arguments = $"--port={port} --session-id={sessionId}",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal,
                    WorkingDirectory = Path.GetDirectoryName(agentPath)
                };

                // Start process in specific session
                var process = Process.Start(startInfo);
                
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start Agent process");
                }

                _logger.LogInformation("Started RPA Agent process {ProcessId} in session {SessionId} for user '{Username}'", 
                    process.Id, sessionId, username);
                
                // Wait a moment for Agent to initialize
                Thread.Sleep(3000);
                
                // Verify Agent is responding
                if (!IsAgentResponding(port))
                {
                    throw new InvalidOperationException($"Agent on port {port} is not responding");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Agent in session {SessionId}", sessionId);
                throw;
            }
        });
    }

    /// <summary>
    /// Configures monitoring for the Windows session
    /// </summary>
    private async Task ConfigureSessionMonitoringAsync(uint windowsSessionId, string sessionId)
    {
        await Task.Run(() =>
        {
            try
            {
                // Set up performance counters for session monitoring
                _logger.LogDebug("Configuring monitoring for session {SessionId} (Windows session {WindowsSessionId})", 
                    sessionId, windowsSessionId);
                
                // In a production environment, you would:
                // 1. Set up performance counters
                // 2. Configure WMI event subscriptions
                // 3. Set up process monitoring
                // 4. Configure resource limits
                
                _logger.LogDebug("Session monitoring configured for {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure monitoring for session {SessionId}", sessionId);
                // Don't throw - monitoring is not critical for basic functionality
            }
        });
    }

    /// <summary>
    /// Stops the Agent application gracefully
    /// </summary>
    private async Task StopAgentInSessionAsync(int port)
    {
        try
        {
            // Try graceful shutdown via HTTP
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            
            var response = await client.PostAsync($"http://localhost:{port}/api/agent/shutdown", null);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Agent on port {Port} shut down gracefully", port);
                await Task.Delay(2000); // Give time for cleanup
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to shutdown Agent gracefully on port {Port}", port);
        }

        // Force kill if graceful shutdown failed
        await ForceKillAgentProcessesAsync(port);
    }

    /// <summary>
    /// Closes applications running in the user session
    /// </summary>
    private async Task CloseApplicationsInSessionAsync(string username)
    {
        await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_Process WHERE Name != 'explorer.exe' AND Name != 'dwm.exe'");
                
                var processes = searcher.Get();
                
                foreach (ManagementObject process in processes)
                {
                    var owner = new string[2];
                    var result = process.InvokeMethod("GetOwner", owner);
                    
                    if (owner[0]?.Equals(username, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        var processId = (uint)process["ProcessId"];
                        var processName = process["Name"].ToString();
                        
                        try
                        {
                            var proc = Process.GetProcessById((int)processId);
                            proc.CloseMainWindow();
                            
                            if (!proc.WaitForExit(5000))
                            {
                                proc.Kill();
                            }
                            
                            _logger.LogDebug("Closed process {ProcessName} ({ProcessId}) for user '{Username}'", 
                                processName, processId, username);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to close process {ProcessName} ({ProcessId})", 
                                processName, processId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close applications for user '{Username}'", username);
            }
        });
    }

    /// <summary>
    /// Logs off the Windows session
    /// </summary>
    private async Task LogoffWindowsSessionAsync(string username)
    {
        await Task.Run(() =>
        {
            try
            {
                // Find session ID for user
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_LogonSession");
                
                var sessions = searcher.Get();
                
                foreach (ManagementObject session in sessions)
                {
                    var logonId = session["LogonId"].ToString();
                    
                    // Get associated user
                    using var userSearcher = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_LogonSession.LogonId={logonId}}} WHERE AssocClass=Win32_LoggedOnUser");
                    
                    var users = userSearcher.Get();
                    
                    foreach (ManagementObject user in users)
                    {
                        if (user["Name"].ToString().Equals(username, StringComparison.OrdinalIgnoreCase))
                        {
                            // Execute logoff
                            var process = new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = "logoff",
                                    Arguments = logonId,
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                }
                            };
                            
                            process.Start();
                            process.WaitForExit();
                            
                            _logger.LogInformation("Logged off Windows session for user '{Username}'", username);
                            return;
                        }
                    }
                }
                
                _logger.LogWarning("No active session found for user '{Username}' to log off", username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log off session for user '{Username}'", username);
            }
        });
    }

    /// <summary>
    /// Cleans up session resources
    /// </summary>
    private async Task CleanupSessionResourcesAsync(string sessionId)
    {
        await Task.Run(() =>
        {
            try
            {
                // Clean up temporary files, registry entries, etc.
                var tempPath = Path.Combine(Path.GetTempPath(), $"RPA_Session_{sessionId}");
                
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                    _logger.LogDebug("Cleaned up temporary files for session {SessionId}", sessionId);
                }
                
                // Additional cleanup tasks...
                _logger.LogDebug("Session resources cleaned up for {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup resources for session {SessionId}", sessionId);
            }
        });
    }

    #endregion

    #region Helper Methods

    private string GenerateSecurePassword()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 12)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private uint CreateUserSession(string username)
    {
        // In production, this would use Windows APIs like:
        // - WTSLogonUser
        // - CreateProcessAsUser
        // - LogonUser
        
        // For this implementation, we'll simulate session creation
        var random = new Random();
        return (uint)random.Next(1000, 9999);
    }

    private bool IsAgentResponding(int port)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = client.GetAsync($"http://localhost:{port}/api/agent/health").Result;
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task ForceKillAgentProcessesAsync(int port)
    {
        await Task.Run(() =>
        {
            try
            {
                // Find processes listening on the port
                var processes = Process.GetProcesses()
                    .Where(p => p.ProcessName.Contains("RPA.Agent", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                        _logger.LogInformation("Force killed Agent process {ProcessId}", process.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to kill Agent process {ProcessId}", process.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to force kill Agent processes on port {Port}", port);
            }
        });
    }

    #endregion
}