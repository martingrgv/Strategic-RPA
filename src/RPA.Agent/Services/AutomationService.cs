using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using RPA.Agent.Models;
using System.Diagnostics;

namespace RPA.Agent.Services;

public interface IAutomationService
{
    Task<ExecutionResult> ExecuteJobAsync(AutomationJob job);
    Task<ExecutionResult> ExecuteStepAsync(AutomationStep step, Application? application = null);
    Task<string> TakeScreenshotAsync(string? fileName = null);
    Task<Application?> StartApplicationAsync(string applicationPath, string? arguments = null);
    Task CloseApplicationAsync(Application application);
}

public class AutomationService : IAutomationService
{
    private readonly ILogger<AutomationService> _logger;
    private readonly UIA3Automation _automation;
    private readonly string _screenshotDirectory;

    public AutomationService(ILogger<AutomationService> logger)
    {
        _logger = logger;
        _automation = new UIA3Automation();
        _screenshotDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
        Directory.CreateDirectory(_screenshotDirectory);
    }

    public async Task<ExecutionResult> ExecuteJobAsync(AutomationJob job)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ExecutionResult();
        Application? application = null;

        try
        {
            _logger.LogInformation("Starting execution of job {JobId} '{JobName}'", job.Id, job.Name);
            job.Status = JobStatus.Running;
            job.StartedAt = DateTime.UtcNow;

            // Start the application if specified
            if (!string.IsNullOrEmpty(job.ApplicationPath))
            {
                application = await StartApplicationAsync(job.ApplicationPath, job.Arguments);
                if (application == null)
                {
                    throw new InvalidOperationException($"Failed to start application: {job.ApplicationPath}");
                }

                // Wait for application to be ready
                await Task.Delay(2000);
            }

            // Take initial screenshot
            var initialScreenshot = await TakeScreenshotAsync($"{job.Id}_start");
            job.Screenshots.Add(initialScreenshot);

            // Execute each step
            var stepResults = new List<ExecutionResult>();
            foreach (var step in job.Steps)
            {
                _logger.LogDebug("Executing step '{StepName}' of type {StepType}", step.Name, step.Type);
                
                var stepResult = await ExecuteStepAsync(step, application);
                stepResults.Add(stepResult);

                if (!stepResult.Success && !step.IsOptional)
                {
                    throw new InvalidOperationException($"Step '{step.Name}' failed: {stepResult.ErrorDetails}");
                }

                // Take screenshot after each step
                if (stepResult.Success)
                {
                    var stepScreenshot = await TakeScreenshotAsync($"{job.Id}_{step.Name}");
                    job.Screenshots.Add(stepScreenshot);
                }

                // Small delay between steps
                await Task.Delay(500);
            }

            // Take final screenshot
            var finalScreenshot = await TakeScreenshotAsync($"{job.Id}_end");
            job.Screenshots.Add(finalScreenshot);

            result.Success = true;
            result.Message = $"Job completed successfully. Executed {stepResults.Count} steps.";
            result.Data["steps"] = stepResults;

            job.Status = JobStatus.Success;
            job.Result = result.Message;

            _logger.LogInformation("Job {JobId} completed successfully in {Duration}ms", 
                job.Id, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing job {JobId}: {Error}", job.Id, ex.Message);
            
            result.Success = false;
            result.ErrorDetails = ex.Message;
            result.Message = "Job execution failed";

            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;

            // Take error screenshot
            try
            {
                var errorScreenshot = await TakeScreenshotAsync($"{job.Id}_error");
                job.Screenshots.Add(errorScreenshot);
            }
            catch (Exception screenshotEx)
            {
                _logger.LogWarning(screenshotEx, "Failed to take error screenshot");
            }
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
            job.CompletedAt = DateTime.UtcNow;

            // Close application if we started it
            if (application != null)
            {
                try
                {
                    await CloseApplicationAsync(application);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing application");
                }
            }

            _automation?.Dispose();
        }

        return result;
    }

    public async Task<ExecutionResult> ExecuteStepAsync(AutomationStep step, Application? application = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ExecutionResult();

        try
        {
            switch (step.Type)
            {
                case StepType.Click:
                    await ExecuteClickAsync(step, application, result);
                    break;

                case StepType.DoubleClick:
                    await ExecuteDoubleClickAsync(step, application, result);
                    break;

                case StepType.Type:
                    await ExecuteTypeAsync(step, application, result);
                    break;

                case StepType.KeyPress:
                    await ExecuteKeyPressAsync(step, result);
                    break;

                case StepType.WaitForElement:
                    await ExecuteWaitForElementAsync(step, application, result);
                    break;

                case StepType.TakeScreenshot:
                    await ExecuteTakeScreenshotAsync(step, result);
                    break;

                case StepType.GetText:
                    await ExecuteGetTextAsync(step, application, result);
                    break;

                case StepType.SetText:
                    await ExecuteSetTextAsync(step, application, result);
                    break;

                case StepType.Validate:
                    await ExecuteValidateAsync(step, application, result);
                    break;

                default:
                    throw new NotSupportedException($"Step type {step.Type} is not supported");
            }

            if (!result.Success)
            {
                _logger.LogWarning("Step '{StepName}' failed: {Error}", step.Name, result.ErrorDetails);
            }
            else
            {
                _logger.LogDebug("Step '{StepName}' completed successfully", step.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing step '{StepName}': {Error}", step.Name, ex.Message);
            result.Success = false;
            result.ErrorDetails = ex.Message;
            result.Message = $"Step execution failed: {ex.Message}";
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<string> TakeScreenshotAsync(string? fileName = null)
    {
        await Task.CompletedTask;
        
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var screenshotName = fileName ?? $"screenshot_{timestamp}";
        var filePath = Path.Combine(_screenshotDirectory, $"{screenshotName}.png");

        try
        {
            using var bitmap = FlaUI.Core.Capturing.Capture.Screen();
            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
            _logger.LogDebug("Screenshot saved to {FilePath}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to take screenshot");
            return string.Empty;
        }
    }

    public async Task<Application?> StartApplicationAsync(string applicationPath, string? arguments = null)
    {
        try
        {
            _logger.LogInformation("Starting application: {ApplicationPath} {Arguments}", 
                applicationPath, arguments ?? "");

            var processInfo = new ProcessStartInfo
            {
                FileName = applicationPath,
                Arguments = arguments ?? "",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };

            var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start process");
            }

            var application = Application.Attach(process);
            
            // Wait for the application to be responsive
            await Task.Delay(2000);
            
            _logger.LogInformation("Application started successfully with PID {ProcessId}", process.Id);
            return application;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start application {ApplicationPath}: {Error}", 
                applicationPath, ex.Message);
            return null;
        }
    }

    public async Task CloseApplicationAsync(Application application)
    {
        try
        {
            await Task.CompletedTask;
            
            var mainWindow = application.GetMainWindow(_automation);
            if (mainWindow != null)
            {
                mainWindow.Close();
                _logger.LogDebug("Application closed gracefully");
            }
            else
            {
                application.Close();
                _logger.LogDebug("Application closed forcefully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing application gracefully, forcing termination");
            try
            {
                application.Kill();
            }
            catch (Exception killEx)
            {
                _logger.LogError(killEx, "Failed to force terminate application");
            }
        }
        finally
        {
            application?.Dispose();
        }
    }

    private async Task ExecuteClickAsync(AutomationStep step, Application? application, ExecutionResult result)
    {
        await Task.CompletedTask;
        
        var element = await FindElementAsync(step.ElementSelector, application);
        if (element == null)
        {
            result.Success = false;
            result.ErrorDetails = $"Element not found: {step.ElementSelector}";
            return;
        }

        element.Click();
        result.Success = true;
        result.Message = $"Clicked element: {step.ElementSelector}";
    }

    private async Task ExecuteDoubleClickAsync(AutomationStep step, Application? application, ExecutionResult result)
    {
        await Task.CompletedTask;
        
        var element = await FindElementAsync(step.ElementSelector, application);
        if (element == null)
        {
            result.Success = false;
            result.ErrorDetails = $"Element not found: {step.ElementSelector}";
            return;
        }

        element.DoubleClick();
        result.Success = true;
        result.Message = $"Double-clicked element: {step.ElementSelector}";
    }

    private async Task ExecuteTypeAsync(AutomationStep step, Application? application, ExecutionResult result)
    {
        await Task.CompletedTask;
        
        if (string.IsNullOrEmpty(step.InputData))
        {
            result.Success = false;
            result.ErrorDetails = "Input data is required for Type step";
            return;
        }

        var element = await FindElementAsync(step.ElementSelector, application);
        if (element != null)
        {
            element.Focus();
            await Task.Delay(100);
        }

        // Type the text
        FlaUI.Core.Input.Keyboard.Type(step.InputData);
        
        result.Success = true;
        result.Message = $"Typed text: {step.InputData}";
    }

    private async Task ExecuteKeyPressAsync(AutomationStep step, ExecutionResult result)
    {
        await Task.CompletedTask;
        
        if (!step.Parameters.ContainsKey("Key"))
        {
            result.Success = false;
            result.ErrorDetails = "Key parameter is required for KeyPress step";
            return;
        }

        var keyString = step.Parameters["Key"].ToString();
        if (Enum.TryParse<FlaUI.Core.WindowsAPI.VirtualKeyShort>(keyString, out var key))
        {
            FlaUI.Core.Input.Keyboard.Press(key);
            result.Success = true;
            result.Message = $"Pressed key: {keyString}";
        }
        else
        {
            result.Success = false;
            result.ErrorDetails = $"Invalid key: {keyString}";
        }
    }

    private async Task ExecuteWaitForElementAsync(AutomationStep step, Application? application, ExecutionResult result)
    {
        var element = await FindElementAsync(step.ElementSelector, application, step.TimeoutMs);
        result.Success = element != null;
        result.Message = element != null ? 
            $"Element found: {step.ElementSelector}" : 
            $"Element not found within timeout: {step.ElementSelector}";
    }

    private async Task ExecuteTakeScreenshotAsync(AutomationStep step, ExecutionResult result)
    {
        var fileName = step.Parameters.ContainsKey("FileName") ? 
            step.Parameters["FileName"].ToString() : null;
        
        var screenshotPath = await TakeScreenshotAsync(fileName);
        result.Success = !string.IsNullOrEmpty(screenshotPath);
        result.ScreenshotPath = screenshotPath;
        result.Message = result.Success ? $"Screenshot saved: {screenshotPath}" : "Failed to take screenshot";
    }

    private async Task ExecuteGetTextAsync(AutomationStep step, Application? application, ExecutionResult result)
    {
        await Task.CompletedTask;
        
        var element = await FindElementAsync(step.ElementSelector, application);
        if (element == null)
        {
            result.Success = false;
            result.ErrorDetails = $"Element not found: {step.ElementSelector}";
            return;
        }

        var text = element.Name ?? element.AsTextBox()?.Text ?? "";
        result.Success = true;
        result.Message = $"Retrieved text: {text}";
        result.Data["text"] = text;
    }

    private async Task ExecuteSetTextAsync(AutomationStep step, Application? application, ExecutionResult result)
    {
        await Task.CompletedTask;
        
        var element = await FindElementAsync(step.ElementSelector, application);
        if (element == null)
        {
            result.Success = false;
            result.ErrorDetails = $"Element not found: {step.ElementSelector}";
            return;
        }

        if (string.IsNullOrEmpty(step.InputData))
        {
            result.Success = false;
            result.ErrorDetails = "Input data is required for SetText step";
            return;
        }

        var textBox = element.AsTextBox();
        if (textBox != null)
        {
            textBox.Text = step.InputData;
            result.Success = true;
            result.Message = $"Set text: {step.InputData}";
        }
        else
        {
            result.Success = false;
            result.ErrorDetails = "Element is not a text input";
        }
    }

    private async Task ExecuteValidateAsync(AutomationStep step, Application? application, ExecutionResult result)
    {
        await Task.CompletedTask;
        
        var element = await FindElementAsync(step.ElementSelector, application);
        if (element == null)
        {
            result.Success = false;
            result.ErrorDetails = $"Element not found for validation: {step.ElementSelector}";
            return;
        }

        if (!step.Parameters.ContainsKey("ExpectedValue"))
        {
            result.Success = false;
            result.ErrorDetails = "ExpectedValue parameter is required for Validate step";
            return;
        }

        var expectedValue = step.Parameters["ExpectedValue"].ToString();
        var actualValue = element.Name ?? element.AsTextBox()?.Text ?? "";
        
        result.Success = string.Equals(expectedValue, actualValue, StringComparison.OrdinalIgnoreCase);
        result.Message = result.Success ? 
            $"Validation passed: {actualValue}" : 
            $"Validation failed: Expected '{expectedValue}', but got '{actualValue}'";
        result.Data["expectedValue"] = expectedValue;
        result.Data["actualValue"] = actualValue;
    }

    protected async Task<AutomationElement?> FindElementAsync(string? selector, Application? application, int timeoutMs = 5000)
    {
        if (string.IsNullOrEmpty(selector))
            return null;

        await Task.CompletedTask;
        
        var endTime = DateTime.Now.AddMilliseconds(timeoutMs);
        
        while (DateTime.Now < endTime)
        {
            try
            {
                AutomationElement? element = null;

                if (application != null)
                {
                    var mainWindow = application.GetMainWindow(_automation);
                    element = mainWindow?.FindFirstDescendant(cf => 
                        cf.ByName(selector).Or(cf.ByAutomationId(selector)).Or(cf.ByText(selector)));
                }
                else
                {
                    // Search in desktop
                    var desktop = _automation.GetDesktop();
                    element = desktop.FindFirstDescendant(cf => 
                        cf.ByName(selector).Or(cf.ByAutomationId(selector)).Or(cf.ByText(selector)));
                }

                if (element != null && element.IsAvailable)
                {
                    return element;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error finding element {Selector}", selector);
            }

            await Task.Delay(250);
        }

        return null;
    }

    public void Dispose()
    {
        _automation?.Dispose();
    }
}