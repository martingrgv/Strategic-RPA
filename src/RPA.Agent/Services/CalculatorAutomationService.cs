using RPA.Agent.Models;

namespace RPA.Agent.Services;

public interface ICalculatorAutomationService
{
    Task<AutomationJob> CreateSimpleCalculationJobAsync(double number1, double number2, string operation);
    Task<AutomationJob> CreateComplexCalculationJobAsync();
}

public class CalculatorAutomationService : ICalculatorAutomationService
{
    private readonly ILogger<CalculatorAutomationService> _logger;

    public CalculatorAutomationService(ILogger<CalculatorAutomationService> logger)
    {
        _logger = logger;
    }

    public async Task<AutomationJob> CreateSimpleCalculationJobAsync(double number1, double number2, string operation)
    {
        await Task.CompletedTask;

        var job = new AutomationJob
        {
            Name = $"Calculator: {number1} {operation} {number2}",
            ApplicationPath = "calc.exe",
            Arguments = null
        };

        var steps = new List<AutomationStep>
        {
            // Clear calculator (just in case)
            new AutomationStep
            {
                Name = "Clear Calculator",
                Type = StepType.Click,
                ElementSelector = "Clear all",
                Description = "Clear any previous calculations"
            },

            // Enter first number
            new AutomationStep
            {
                Name = "Enter First Number",
                Type = StepType.Custom,
                Description = $"Enter the number {number1}",
                Parameters = new Dictionary<string, object> { ["Number"] = number1 }
            },

            // Click operation
            new AutomationStep
            {
                Name = $"Click {operation}",
                Type = StepType.Click,
                ElementSelector = GetOperationSelector(operation),
                Description = $"Click the {operation} operation"
            },

            // Enter second number
            new AutomationStep
            {
                Name = "Enter Second Number", 
                Type = StepType.Custom,
                Description = $"Enter the number {number2}",
                Parameters = new Dictionary<string, object> { ["Number"] = number2 }
            },

            // Click equals
            new AutomationStep
            {
                Name = "Click Equals",
                Type = StepType.Click,
                ElementSelector = "Equals",
                Description = "Calculate the result"
            },

            // Get result
            new AutomationStep
            {
                Name = "Get Result",
                Type = StepType.GetText,
                ElementSelector = "CalculatorResults",
                Description = "Retrieve the calculation result"
            },

            // Take screenshot of result
            new AutomationStep
            {
                Name = "Screenshot Result",
                Type = StepType.TakeScreenshot,
                Description = "Capture the final result",
                Parameters = new Dictionary<string, object> 
                { 
                    ["FileName"] = $"calc_result_{number1}_{operation}_{number2}" 
                }
            },

            // Validate result (optional)
            new AutomationStep
            {
                Name = "Validate Result",
                Type = StepType.Validate,
                ElementSelector = "CalculatorResults",
                IsOptional = true,
                Description = "Verify the calculation result is correct",
                Parameters = new Dictionary<string, object> 
                { 
                    ["ExpectedValue"] = CalculateExpectedResult(number1, number2, operation).ToString()
                }
            }
        };

        job.Steps = steps;
        return job;
    }

    public async Task<AutomationJob> CreateComplexCalculationJobAsync()
    {
        await Task.CompletedTask;

        var job = new AutomationJob
        {
            Name = "Complex Calculator Operations",
            ApplicationPath = "calc.exe",
            Arguments = null
        };

        var steps = new List<AutomationStep>
        {
            // Clear calculator
            new AutomationStep
            {
                Name = "Clear Calculator",
                Type = StepType.Click,
                ElementSelector = "Clear all",
                Description = "Clear calculator for fresh start"
            },

            // Calculate: (25 + 15) * 2 = 80
            
            // Enter 25
            new AutomationStep
            {
                Name = "Enter 25",
                Type = StepType.Custom,
                Description = "Enter the number 25",
                Parameters = new Dictionary<string, object> { ["Number"] = 25.0 }
            },

            // Click plus
            new AutomationStep
            {
                Name = "Click Plus",
                Type = StepType.Click,
                ElementSelector = "Plus",
                Description = "Click addition operator"
            },

            // Enter 15  
            new AutomationStep
            {
                Name = "Enter 15",
                Type = StepType.Custom,
                Description = "Enter the number 15",
                Parameters = new Dictionary<string, object> { ["Number"] = 15.0 }
            },

            // Click equals to get intermediate result (40)
            new AutomationStep
            {
                Name = "Click Equals",
                Type = StepType.Click,
                ElementSelector = "Equals", 
                Description = "Get intermediate result"
            },

            // Take screenshot of intermediate result
            new AutomationStep
            {
                Name = "Screenshot Intermediate",
                Type = StepType.TakeScreenshot,
                Description = "Capture intermediate result (25+15=40)",
                Parameters = new Dictionary<string, object> 
                { 
                    ["FileName"] = "calc_intermediate_25_plus_15" 
                }
            },

            // Click multiply
            new AutomationStep
            {
                Name = "Click Multiply",
                Type = StepType.Click,
                ElementSelector = "Multiply by",
                Description = "Click multiplication operator"
            },

            // Enter 2
            new AutomationStep
            {
                Name = "Enter 2",
                Type = StepType.Custom,
                Description = "Enter the number 2",
                Parameters = new Dictionary<string, object> { ["Number"] = 2.0 }
            },

            // Click equals for final result
            new AutomationStep
            {
                Name = "Click Equals Final",
                Type = StepType.Click,
                ElementSelector = "Equals",
                Description = "Calculate final result"
            },

            // Get final result
            new AutomationStep
            {
                Name = "Get Final Result",
                Type = StepType.GetText,
                ElementSelector = "CalculatorResults",
                Description = "Retrieve final calculation result"
            },

            // Take screenshot of final result
            new AutomationStep
            {
                Name = "Screenshot Final Result",
                Type = StepType.TakeScreenshot,
                Description = "Capture final result (40*2=80)",
                Parameters = new Dictionary<string, object> 
                { 
                    ["FileName"] = "calc_final_result_complex" 
                }
            },

            // Validate final result
            new AutomationStep
            {
                Name = "Validate Final Result",
                Type = StepType.Validate,
                ElementSelector = "CalculatorResults",
                Description = "Verify the final result is 80",
                Parameters = new Dictionary<string, object> 
                { 
                    ["ExpectedValue"] = "80"
                }
            },

            // Test memory functions
            
            // Store result in memory (MS)
            new AutomationStep
            {
                Name = "Memory Store",
                Type = StepType.Click,
                ElementSelector = "Memory store",
                Description = "Store current result (80) in memory",
                IsOptional = true
            },

            // Clear calculator
            new AutomationStep
            {
                Name = "Clear for Memory Test",
                Type = StepType.Click,
                ElementSelector = "Clear all",
                Description = "Clear calculator to test memory recall"
            },

            // Enter 20
            new AutomationStep
            {
                Name = "Enter 20",
                Type = StepType.Custom,
                Description = "Enter the number 20",
                Parameters = new Dictionary<string, object> { ["Number"] = 20.0 }
            },

            // Click plus
            new AutomationStep
            {
                Name = "Click Plus for Memory",
                Type = StepType.Click,
                ElementSelector = "Plus",
                Description = "Click addition for memory calculation"
            },

            // Recall from memory (MR)
            new AutomationStep
            {
                Name = "Memory Recall",
                Type = StepType.Click,
                ElementSelector = "Memory recall",
                Description = "Recall stored value (80) from memory",
                IsOptional = true
            },

            // Click equals
            new AutomationStep
            {
                Name = "Click Equals Memory",
                Type = StepType.Click,
                ElementSelector = "Equals",
                Description = "Calculate 20 + 80 = 100"
            },

            // Get memory calculation result
            new AutomationStep
            {
                Name = "Get Memory Result",
                Type = StepType.GetText,
                ElementSelector = "CalculatorResults",
                Description = "Get memory calculation result"
            },

            // Final screenshot
            new AutomationStep
            {
                Name = "Screenshot Memory Result",
                Type = StepType.TakeScreenshot,
                Description = "Capture memory calculation result (20+80=100)",
                Parameters = new Dictionary<string, object> 
                { 
                    ["FileName"] = "calc_memory_result" 
                }
            }
        };

        job.Steps = steps;
        return job;
    }

    private string GetOperationSelector(string operation)
    {
        return operation.ToLowerInvariant() switch
        {
            "+" or "add" or "plus" => "Plus",
            "-" or "subtract" or "minus" => "Minus",
            "*" or "multiply" or "times" => "Multiply by",
            "/" or "divide" => "Divide by",
            _ => throw new ArgumentException($"Unsupported operation: {operation}")
        };
    }

    private double CalculateExpectedResult(double number1, double number2, string operation)
    {
        return operation.ToLowerInvariant() switch
        {
            "+" or "add" or "plus" => number1 + number2,
            "-" or "subtract" or "minus" => number1 - number2,
            "*" or "multiply" or "times" => number1 * number2,
            "/" or "divide" => number2 != 0 ? number1 / number2 : throw new DivideByZeroException(),
            _ => throw new ArgumentException($"Unsupported operation: {operation}")
        };
    }
}

// Enhanced automation service that handles number entry for calculator
public class EnhancedAutomationService : AutomationService
{
    public EnhancedAutomationService(ILogger<AutomationService> logger) : base(logger)
    {
    }

    public override async Task<ExecutionResult> ExecuteStepAsync(AutomationStep step, Application? application = null)
    {
        // Handle custom number entry for calculator
        if (step.Type == StepType.Custom && step.Parameters.ContainsKey("Number"))
        {
            return await ExecuteNumberEntryAsync(step, application);
        }

        return await base.ExecuteStepAsync(step, application);
    }

    private async Task<ExecutionResult> ExecuteNumberEntryAsync(AutomationStep step, Application? application)
    {
        var result = new ExecutionResult();
        
        try
        {
            if (!step.Parameters.TryGetValue("Number", out var numberObj) || 
                !double.TryParse(numberObj.ToString(), out var number))
            {
                result.Success = false;
                result.ErrorDetails = "Invalid number parameter";
                return result;
            }

            // Convert number to string and handle decimal points
            var numberString = number.ToString();
            
            foreach (var digit in numberString)
            {
                var elementSelector = digit switch
                {
                    '0' => "Zero",
                    '1' => "One", 
                    '2' => "Two",
                    '3' => "Three",
                    '4' => "Four",
                    '5' => "Five",
                    '6' => "Six", 
                    '7' => "Seven",
                    '8' => "Eight",
                    '9' => "Nine",
                    '.' => "Decimal separator",
                    _ => throw new ArgumentException($"Unsupported character: {digit}")
                };

                // Find and click the digit button
                var element = await FindElementAsync(elementSelector, application);
                if (element == null)
                {
                    result.Success = false;
                    result.ErrorDetails = $"Could not find button for: {digit}";
                    return result;
                }

                element.Click();
                await Task.Delay(100); // Small delay between clicks
            }

            result.Success = true;
            result.Message = $"Entered number: {number}";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorDetails = ex.Message;
            result.Message = "Failed to enter number";
        }

        return result;
    }
}