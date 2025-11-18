using RPA.Agent.Models;

namespace RPA.Agent.Services;

public static class CalculatorAutomation
{
    public static AutomationJob CreateSimpleCalculation()
    {
        return new AutomationJob
        {
            Name = "Calculator Simple Addition",
            ApplicationPath = "calc.exe",
            Steps = new List<AutomationStep>
            {
                new AutomationStep
                {
                    Name = "Wait for Calculator to Load",
                    Type = StepType.WaitForElement,
                    ElementSelector = "Name=Calculator",
                    TimeoutMs = 5000,
                    Description = "Wait for calculator window to appear"
                },
                new AutomationStep
                {
                    Name = "Click Number 5",
                    Type = StepType.Click,
                    ElementSelector = "Name=Five",
                    Description = "Click the number 5 button"
                },
                new AutomationStep
                {
                    Name = "Click Plus",
                    Type = StepType.Click,
                    ElementSelector = "Name=Plus",
                    Description = "Click the plus operator"
                },
                new AutomationStep
                {
                    Name = "Click Number 3",
                    Type = StepType.Click,
                    ElementSelector = "Name=Three", 
                    Description = "Click the number 3 button"
                },
                new AutomationStep
                {
                    Name = "Click Equals",
                    Type = StepType.Click,
                    ElementSelector = "Name=Equals",
                    Description = "Click equals to get result"
                },
                new AutomationStep
                {
                    Name = "Take Screenshot of Result",
                    Type = StepType.TakeScreenshot,
                    Parameters = new Dictionary<string, object>
                    {
                        ["FileName"] = "calculator_result"
                    },
                    Description = "Capture the calculation result"
                },
                new AutomationStep
                {
                    Name = "Validate Result",
                    Type = StepType.Validate,
                    ElementSelector = "AutomationId=CalculatorResults",
                    Parameters = new Dictionary<string, object>
                    {
                        ["ExpectedValue"] = "8"
                    },
                    Description = "Verify the result is 8"
                }
            }
        };
    }

    public static AutomationJob CreateComplexCalculation()
    {
        return new AutomationJob
        {
            Name = "Calculator Complex Operations",
            ApplicationPath = "calc.exe",
            Steps = new List<AutomationStep>
            {
                new AutomationStep
                {
                    Name = "Wait for Calculator",
                    Type = StepType.WaitForElement,
                    ElementSelector = "Name=Calculator",
                    TimeoutMs = 5000
                },
                new AutomationStep
                {
                    Name = "Clear Calculator",
                    Type = StepType.Click,
                    ElementSelector = "Name=Clear",
                    IsOptional = true,
                    Description = "Clear any previous calculations"
                },
                new AutomationStep
                {
                    Name = "Enter 15",
                    Type = StepType.Click,
                    ElementSelector = "Name=One",
                    Description = "Click 1"
                },
                new AutomationStep
                {
                    Name = "Enter 5",
                    Type = StepType.Click,
                    ElementSelector = "Name=Five",
                    Description = "Click 5 to make 15"
                },
                new AutomationStep
                {
                    Name = "Multiply",
                    Type = StepType.Click,
                    ElementSelector = "Name=Multiply by",
                    Description = "Click multiply operator"
                },
                new AutomationStep
                {
                    Name = "Enter 2",
                    Type = StepType.Click,
                    ElementSelector = "Name=Two",
                    Description = "Click 2"
                },
                new AutomationStep
                {
                    Name = "Calculate Result",
                    Type = StepType.Click,
                    ElementSelector = "Name=Equals",
                    Description = "Calculate 15 * 2"
                },
                new AutomationStep
                {
                    Name = "Add 10",
                    Type = StepType.Click,
                    ElementSelector = "Name=Plus",
                    Description = "Add operation"
                },
                new AutomationStep
                {
                    Name = "Enter 10 - First Digit",
                    Type = StepType.Click,
                    ElementSelector = "Name=One",
                    Description = "Click 1"
                },
                new AutomationStep
                {
                    Name = "Enter 10 - Second Digit",
                    Type = StepType.Click,
                    ElementSelector = "Name=Zero",
                    Description = "Click 0 to make 10"
                },
                new AutomationStep
                {
                    Name = "Final Calculation",
                    Type = StepType.Click,
                    ElementSelector = "Name=Equals",
                    Description = "Final result: 30 + 10 = 40"
                },
                new AutomationStep
                {
                    Name = "Screenshot Final Result",
                    Type = StepType.TakeScreenshot,
                    Parameters = new Dictionary<string, object>
                    {
                        ["FileName"] = "calculator_complex_result"
                    }
                },
                new AutomationStep
                {
                    Name = "Validate Final Result",
                    Type = StepType.Validate,
                    ElementSelector = "AutomationId=CalculatorResults",
                    Parameters = new Dictionary<string, object>
                    {
                        ["ExpectedValue"] = "40"
                    },
                    Description = "Verify final result is 40"
                }
            }
        };
    }

    public static AutomationJob CreateScientificCalculation()
    {
        return new AutomationJob
        {
            Name = "Calculator Scientific Mode",
            ApplicationPath = "calc.exe",
            Steps = new List<AutomationStep>
            {
                new AutomationStep
                {
                    Name = "Wait for Calculator",
                    Type = StepType.WaitForElement,
                    ElementSelector = "Name=Calculator",
                    TimeoutMs = 5000
                },
                new AutomationStep
                {
                    Name = "Open Menu",
                    Type = StepType.Click,
                    ElementSelector = "AutomationId=TogglePaneButton",
                    IsOptional = true,
                    Description = "Open navigation menu"
                },
                new AutomationStep
                {
                    Name = "Switch to Scientific",
                    Type = StepType.Click,
                    ElementSelector = "Name=Scientific Calculator",
                    IsOptional = true,
                    Description = "Switch to scientific mode"
                },
                new AutomationStep
                {
                    Name = "Enter 16",
                    Type = StepType.Click,
                    ElementSelector = "Name=One",
                    Description = "Click 1"
                },
                new AutomationStep
                {
                    Name = "Complete 16",
                    Type = StepType.Click,
                    ElementSelector = "Name=Six",
                    Description = "Click 6 to make 16"
                },
                new AutomationStep
                {
                    Name = "Square Root",
                    Type = StepType.Click,
                    ElementSelector = "Name=Square root",
                    Description = "Calculate square root of 16"
                },
                new AutomationStep
                {
                    Name = "Screenshot Square Root",
                    Type = StepType.TakeScreenshot,
                    Parameters = new Dictionary<string, object>
                    {
                        ["FileName"] = "calculator_sqrt_result"
                    }
                },
                new AutomationStep
                {
                    Name = "Validate Square Root",
                    Type = StepType.Validate,
                    ElementSelector = "AutomationId=CalculatorResults",
                    Parameters = new Dictionary<string, object>
                    {
                        ["ExpectedValue"] = "4"
                    },
                    Description = "Verify sqrt(16) = 4",
                    IsOptional = true // Element selector might vary by Windows version
                }
            }
        };
    }
}