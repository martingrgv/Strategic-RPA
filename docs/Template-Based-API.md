# Simplified Strategic RPA API - Template-Based Automation

## Overview
The Strategic RPA API now supports **predefined automation templates** that clients can execute with simple parameters, rather than defining complex automation steps.

## Key Changes

### Old Approach (Complex):
```json
POST /api/automation/jobs
{
  "name": "Calculator Test",
  "applicationPath": "calc.exe", 
  "steps": [
    {"order": 1, "type": "Click", "target": "Button[@Name='1']"},
    {"order": 2, "type": "Click", "target": "Button[@Name='+']"},
    {"order": 3, "type": "Click", "target": "Button[@Name='2']"},
    {"order": 4, "type": "Click", "target": "Button[@Name='=']"},
    {"order": 5, "type": "Validate", "target": "Text[@Name='Display']", "value": "3"}
  ]
}
```

### New Approach (Simple):
```json
POST /api/automation/templates/CalculatorAddition/execute
{
  "parameters": {
    "num1": 1,
    "num2": 2
  }
}
```

## Available Templates

### 1. Calculator Addition
**Template ID**: `CalculatorAddition`
```bash
curl -X POST http://localhost:5000/api/automation/templates/CalculatorAddition/execute \
  -H "Content-Type: application/json" \
  -d '{
    "parameters": {
      "num1": 5,
      "num2": 3
    },
    "priority": "Normal"
  }'
```

### 2. Calculator Multiplication  
**Template ID**: `CalculatorMultiplication`
```bash
curl -X POST http://localhost:5000/api/automation/templates/CalculatorMultiplication/execute \
  -H "Content-Type: application/json" \
  -d '{
    "parameters": {
      "num1": 7,
      "num2": 4
    }
  }'
```

### 3. Notepad Text Entry
**Template ID**: `NotepadTextEntry`
```bash
curl -X POST http://localhost:5000/api/automation/templates/NotepadTextEntry/execute \
  -H "Content-Type: application/json" \
  -d '{
    "parameters": {
      "text": "Hello, World!",
      "saveAs": "test.txt"
    }
  }'
```

## API Endpoints

### List Available Templates
```http
GET /api/automation/templates
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": "CalculatorAddition",
      "name": "Calculator Addition",
      "description": "Performs addition operation using Windows Calculator",
      "applicationPath": "calc.exe",
      "parameters": [
        {
          "name": "num1",
          "type": "number",
          "required": true,
          "description": "First number"
        },
        {
          "name": "num2", 
          "type": "number",
          "required": true,
          "description": "Second number"
        }
      ]
    }
  ]
}
```

### Get Template Details
```http
GET /api/automation/templates/{templateId}
```

### Execute Template
```http
POST /api/automation/templates/{templateId}/execute
```

**Request Body:**
```json
{
  "parameters": {
    "param1": "value1",
    "param2": "value2"
  },
  "priority": "Normal|High|Low|Critical",
  "webhookUrl": "https://optional-webhook.com/callback"
}
```

### Check Execution Result
```http
GET /api/automation/templates/executions/{jobId}
```

## Programming Examples

### C# Example:
```csharp
var client = new HttpClient();
var request = new
{
    parameters = new { num1 = 10, num2 = 5 },
    priority = "Normal"
};

var json = JsonSerializer.Serialize(request);
var content = new StringContent(json, Encoding.UTF8, "application/json");

var response = await client.PostAsync(
    "http://localhost:5000/api/automation/templates/CalculatorAddition/execute", 
    content);
```

### Python Example:
```python
import requests

response = requests.post(
    'http://localhost:5000/api/automation/templates/CalculatorMultiplication/execute',
    json={
        'parameters': {'num1': 8, 'num2': 7},
        'priority': 'Normal'
    }
)
print(response.json())
```

### JavaScript Example:
```javascript
const response = await fetch('/api/automation/templates/NotepadTextEntry/execute', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
        parameters: {
            text: 'Automated text entry!',
            saveAs: 'automation-test.txt'
        }
    })
});
const result = await response.json();
```

## Benefits

✅ **Simple Integration**: Clients just provide parameters  
✅ **Predefined Logic**: No need to understand UI automation details  
✅ **Consistent Results**: Templates are tested and reliable  
✅ **Parameter Validation**: Required fields are automatically checked  
✅ **Easy Maintenance**: Templates can be updated without client changes  

## Migration Guide

### Before (Complex):
```json
{
  "name": "Add two numbers",
  "applicationPath": "calc.exe",
  "steps": [
    // 5+ complex step definitions...
  ]
}
```

### After (Simple):  
```json
{
  "parameters": {
    "num1": 10,
    "num2": 5
  }
}
```

**Result**: Same automation, 90% less code!