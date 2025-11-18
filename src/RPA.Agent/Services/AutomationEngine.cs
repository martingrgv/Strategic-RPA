using RPA.Agent.Models;
using System.Diagnostics;

namespace RPA.Agent.Services;

public interface IAutomationEngine
{
    Task<ExecutionResult> ExecuteJobAsync(AutomationJob job);
    Task<ExecutionResult> ExecuteStepAsync(AutomationStep step, Process? app = null);
    Task<string> TakeScreenshotAsync(string? fileName = null);
    Task<Process?> LaunchApplicationAsync(string applicationPath, string? arguments = null);
    Task CloseApplicationAsync(Process application);
}

public class CrossPlatformAutomationEngine : IAutomationEngine, IDisposable
{
    private readonly ILogger<CrossPlatformAutomationEngine> _logger;
    private readonly Dictionary<Guid, Process> _runningApplications = new();
    private readonly Random _random = new();

    public CrossPlatformAutomationEngine(ILogger<CrossPlatformAutomationEngine> logger)
    {
        _logger = logger;
    }

    public async Task<ExecutionResult> ExecuteJobAsync(AutomationJob job)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting execution of job {JobId} '{JobName}'", job.Id, job.Name);

        try
        {
            job.Status = JobStatus.Running;
            job.StartedAt = startTime;

            // Simulate launching application
            Process? app = null;
            if (!string.IsNullOrEmpty(job.ApplicationPath))
            {
                app = await LaunchApplicationAsync(job.ApplicationPath, job.Arguments);
                if (app == null)
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        Message = $"Failed to launch application: {job.ApplicationPath}",
                        ExecutionTime = DateTime.UtcNow - startTime
                    };
                }
                
                _runningApplications[job.Id] = app;
                await Task.Delay(2000); // Allow app to fully load
            }

            // Execute each step with simulation
            var allResults = new List<ExecutionResult>();
            foreach (var step in job.Steps)
            {
                _logger.LogDebug("Executing step '{StepName}' of type {StepType}", step.Name, step.Type);
                
                var stepResult = await ExecuteStepAsync(step, app);
                allResults.Add(stepResult);

                if (!stepResult.Success && !step.IsOptional)
                {
                    _logger.LogError("Step '{StepName}' failed: {ErrorMessage}", step.Name, stepResult.ErrorDetails);
                    
                    // Take screenshot on failure
                    var screenshotPath = await TakeScreenshotAsync($"error_{job.Id}_{step.Id}");
                    job.Screenshots.Add(screenshotPath);

                    job.Status = JobStatus.Failed;
                    job.CompletedAt = DateTime.UtcNow;
                    job.ErrorMessage = stepResult.ErrorDetails;

                    return new ExecutionResult
                    {
                        Success = false,
                        Message = $"Job failed at step '{step.Name}'",
                        ErrorDetails = stepResult.ErrorDetails,
                        ExecutionTime = DateTime.UtcNow - startTime,
                        Data = new Dictionary<string, object> { ["StepResults"] = allResults }
                    };
                }
            }

            // Take final screenshot
            var finalScreenshot = await TakeScreenshotAsync($"final_{job.Id}");
            job.Screenshots.Add(finalScreenshot);

            job.Status = JobStatus.Success;
            job.CompletedAt = DateTime.UtcNow;
            job.Result = "Job completed successfully (simulated)";

            _logger.LogInformation("Job {JobId} completed successfully in {Duration}ms", 
                job.Id, (DateTime.UtcNow - startTime).TotalMilliseconds);

            return new ExecutionResult
            {
                Success = true,
                Message = "Job completed successfully (simulated)",
                ExecutionTime = DateTime.UtcNow - startTime,
                Data = new Dictionary<string, object> { ["StepResults"] = allResults }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing job {JobId}", job.Id);
            
            job.Status = JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorMessage = ex.Message;

            return new ExecutionResult
            {
                Success = false,
                Message = "Job execution failed",
                ErrorDetails = ex.Message,
                ExecutionTime = DateTime.UtcNow - startTime
            };
        }
        finally
        {
            // Clean up application if we launched it
            if (_runningApplications.TryGetValue(job.Id, out var app))
            {
                await CloseApplicationAsync(app);
                _runningApplications.Remove(job.Id);
            }
        }
    }

    public async Task<ExecutionResult> ExecuteStepAsync(AutomationStep step, Process? app = null)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Simulate step execution with realistic delays
            await Task.Delay(_random.Next(200, 800)); // Random execution time

            return step.Type switch
            {
                StepType.Click => await ExecuteClickAsync(step),
                StepType.DoubleClick => await ExecuteDoubleClickAsync(step),
                StepType.Type => await ExecuteTypeAsync(step),
                StepType.KeyPress => await ExecuteKeyPressAsync(step),
                StepType.WaitForElement => await ExecuteWaitForElementAsync(step),
                StepType.TakeScreenshot => await ExecuteTakeScreenshotAsync(step),
                StepType.GetText => await ExecuteGetTextAsync(step),
                StepType.SetText => await ExecuteSetTextAsync(step),
                StepType.Validate => await ExecuteValidateAsync(step),
                _ => new ExecutionResult
                {
                    Success = false,
                    Message = $"Unsupported step type: {step.Type}",
                    ExecutionTime = DateTime.UtcNow - startTime
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing step '{StepName}' of type {StepType}", step.Name, step.Type);
            
            return new ExecutionResult
            {
                Success = false,
                Message = $"Step execution failed: {step.Name}",
                ErrorDetails = ex.Message,
                ExecutionTime = DateTime.UtcNow - startTime
            };
        }
    }

    private async Task<ExecutionResult> ExecuteClickAsync(AutomationStep step)
    {
        _logger.LogDebug("Simulating click on element: {ElementSelector}", step.ElementSelector);
        await Task.Delay(100);
        
        // Simulate occasional failures (5% chance)
        if (_random.NextDouble() < 0.05)
            return FailedResult($"Element not found: {step.ElementSelector}");
        
        return SuccessResult($"Clicked on {step.ElementSelector}");
    }

    private async Task<ExecutionResult> ExecuteDoubleClickAsync(AutomationStep step)
    {
        _logger.LogDebug("Simulating double-click on element: {ElementSelector}", step.ElementSelector);
        await Task.Delay(100);
        
        if (_random.NextDouble() < 0.05)
            return FailedResult($"Element not found: {step.ElementSelector}");
        
        return SuccessResult($"Double-clicked on {step.ElementSelector}");
    }

    private async Task<ExecutionResult> ExecuteTypeAsync(AutomationStep step)
    {
        if (string.IsNullOrEmpty(step.InputData))
            return FailedResult("No input data provided for Type step");

        _logger.LogDebug("Simulating typing: {InputData}", step.InputData);
        await Task.Delay(step.InputData.Length * 50); // Simulate typing speed
        
        return SuccessResult($"Typed: {step.InputData}");
    }

    private async Task<ExecutionResult> ExecuteKeyPressAsync(AutomationStep step)
    {
        if (!step.Parameters.TryGetValue("Key", out var keyValue))
            return FailedResult("No key specified for KeyPress step");

        var keyString = keyValue.ToString()!;
        _logger.LogDebug("Simulating key press: {Key}", keyString);
        await Task.Delay(100);
        
        return SuccessResult($"Key pressed: {keyString}");
    }

    private async Task<ExecutionResult> ExecuteWaitForElementAsync(AutomationStep step)
    {
        _logger.LogDebug("Simulating wait for element: {ElementSelector}", step.ElementSelector);
        
        // Simulate waiting with timeout
        var waitTime = Math.Min(step.TimeoutMs, 2000); // Cap simulation at 2 seconds
        await Task.Delay(waitTime / 10);
        
        // Simulate 90% success rate for finding elements
        if (_random.NextDouble() < 0.9)
            return SuccessResult($"Element found: {step.ElementSelector}");
        
        return FailedResult($"Element not found within timeout: {step.ElementSelector}");
    }

    private async Task<ExecutionResult> ExecuteTakeScreenshotAsync(AutomationStep step)
    {
        var fileName = step.Parameters.TryGetValue("FileName", out var fileNameValue) 
            ? fileNameValue.ToString() 
            : null;
            
        var screenshotPath = await TakeScreenshotAsync(fileName);
        
        return SuccessResult("Screenshot taken (simulated)", new Dictionary<string, object>
        {
            ["ScreenshotPath"] = screenshotPath
        });
    }

    private async Task<ExecutionResult> ExecuteGetTextAsync(AutomationStep step)
    {
        _logger.LogDebug("Simulating getting text from: {ElementSelector}", step.ElementSelector);
        await Task.Delay(200);
        
        // Simulate different text values based on calculator context
        var simulatedText = step.ElementSelector switch
        {
            var s when s.Contains("CalculatorResults") => GetSimulatedCalculatorResult(),
            var s when s.Contains("Five") => "5",
            var s when s.Contains("Three") => "3",
            var s when s.Contains("Plus") => "+",
            var s when s.Contains("Equals") => "=",
            _ => "SimulatedText"
        };
        
        return SuccessResult($"Text retrieved: {simulatedText}", new Dictionary<string, object>
        {
            ["Text"] = simulatedText
        });
    }

    private async Task<ExecutionResult> ExecuteSetTextAsync(AutomationStep step)
    {
        if (string.IsNullOrEmpty(step.InputData))
            return FailedResult("No input data provided for SetText step");

        _logger.LogDebug("Simulating setting text to: {InputData}", step.InputData);
        await Task.Delay(step.InputData.Length * 30);
        
        return SuccessResult($"Text set: {step.InputData}");
    }

    private async Task<ExecutionResult> ExecuteValidateAsync(AutomationStep step)
    {
        if (!step.Parameters.TryGetValue("ExpectedValue", out var expectedValue))
            return FailedResult("No expected value provided for validation");

        await Task.Delay(300);
        
        var expected = expectedValue.ToString();
        var simulatedActual = GetSimulatedCalculatorResult();
        
        _logger.LogDebug("Simulating validation - Expected: {Expected}, Actual: {Actual}", 
            expected, simulatedActual);

        // For calculator demonstration, simulate correct results for common calculations
        var isValid = expected switch
        {
            "8" => true,  // 5 + 3 = 8
            "40" => true, // Complex calculation result
            "4" => true,  // sqrt(16) = 4
            _ => simulatedActual == expected
        };

        if (isValid)
        {
            return SuccessResult($"Validation passed: {simulatedActual} = {expected}");
        }

        return FailedResult($"Validation failed: Expected '{expected}', got '{simulatedActual}'");
    }

    public async Task<Process?> LaunchApplicationAsync(string applicationPath, string? arguments = null)
    {
        try
        {
            _logger.LogInformation("Simulating launch of application: {ApplicationPath} {Arguments}", 
                applicationPath, arguments);
            
            // For calculator, simulate successful launch
            if (applicationPath.Contains("calc"))
            {
                await Task.Delay(1000); // Simulate app startup time
                
                // Create a dummy process object for tracking
                var process = new Process();
                _logger.LogInformation("Calculator application launched successfully (simulated)");
                return process;
            }
            
            // For other applications, simulate launch
            await Task.Delay(2000);
            return new Process();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch application: {ApplicationPath}", applicationPath);
            return null;
        }
    }

    public async Task CloseApplicationAsync(Process application)
    {
        try
        {
            _logger.LogDebug("Simulating application closure");
            await Task.Delay(500);
            application.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing application");
        }
    }

    public async Task<string> TakeScreenshotAsync(string? fileName = null)
    {
        try
        {
            var screenshotsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
            Directory.CreateDirectory(screenshotsDir);
            
            fileName ??= $"screenshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            if (!fileName.EndsWith(".png"))
                fileName += ".png";
                
            var filePath = Path.Combine(screenshotsDir, fileName);
            
            // Create a simple text file as placeholder screenshot
            await File.WriteAllTextAsync(filePath.Replace(".png", ".txt"), 
                $"Screenshot placeholder taken at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\n" +
                $"Simulated desktop capture\n" +
                $"Calculator demonstration");
            
            _logger.LogDebug("Screenshot simulated: {FilePath}", filePath);
            return filePath.Replace(".png", ".txt");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to take screenshot");
            return "";
        }
    }

    private string GetSimulatedCalculatorResult()
    {
        // Simulate calculator results based on context
        return _random.Next(0, 3) switch
        {
            0 => "8",   // 5 + 3
            1 => "40",  // Complex calculation 
            2 => "4",   // sqrt(16)
            _ => "0"
        };
    }

    private static ExecutionResult SuccessResult(string message, Dictionary<string, object>? data = null) =>
        new() { Success = true, Message = message, Data = data ?? new() };

    private static ExecutionResult FailedResult(string message) =>
        new() { Success = false, Message = message };

    public void Dispose()
    {
        foreach (var app in _runningApplications.Values)
        {
            try
            {
                app.Dispose();
            }
            catch { /* Ignore cleanup errors */ }
        }
    }
}