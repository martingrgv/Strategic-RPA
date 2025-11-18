# Strategic RPA API Documentation

## Overview
The Strategic RPA API provides endpoints for creating and managing automation agents that can interact with Windows desktop applications using UI automation.

## Base URL
```
http://localhost:5000 (Development)
```

## Authentication
Currently, no authentication is required (will be added in future versions).

## API Endpoints

### Health Check
```http
GET /health
```
Returns the API health status.

### Agents Management

#### Create Agent
```http
POST /api/agents
```

**Request Body:**
```json
{
  "name": "Agent-Calculator",
  "windowsUser": "AutoAgent01",
  "capabilities": {
    "supportedApplications": ["calc.exe", "notepad.exe"],
    "maxConcurrentJobs": 1,
    "supportsScreenCapture": true,
    "supportsFlaUI": true
  }
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "name": "Agent-Calculator",
    "sessionId": "RDP-Session-12345678",
    "windowsUser": "AutoAgent01",
    "status": "Starting",
    "createdAt": "2025-11-18T10:00:00Z",
    "lastHeartbeat": null,
    "currentJobId": null,
    "jobsExecuted": 0,
    "lastError": null,
    "capabilities": {
      "supportedApplications": ["calc.exe", "notepad.exe"],
      "maxConcurrentJobs": 1,
      "supportsScreenCapture": true,
      "supportsFlaUI": true
    }
  }
}
```

#### Get All Agents
```http
GET /api/agents
```

#### Get Agent by ID
```http
GET /api/agents/{id}
```

#### Get Available Agents
```http
GET /api/agents/available
```

#### Update Agent Status
```http
PATCH /api/agents/{id}/status
```

**Request Body:**
```json
{
  "status": "Idle",
  "currentJobId": null,
  "lastError": null
}
```

#### Send Agent Heartbeat
```http
POST /api/agents/{id}/heartbeat
```

#### Delete Agent
```http
DELETE /api/agents/{id}
```

### Job Management

#### Create Automation Job
```http
POST /api/automation/jobs
```

**Request Body:**
```json
{
  "name": "Calculator Test",
  "applicationPath": "calc.exe",
  "arguments": null,
  "steps": [
    {
      "order": 1,
      "type": "Click",
      "target": "Button[@Name='1']",
      "value": null,
      "timeoutMs": 5000,
      "continueOnError": false,
      "description": "Click number 1"
    },
    {
      "order": 2,
      "type": "Click", 
      "target": "Button[@Name='+']",
      "value": null,
      "timeoutMs": 5000,
      "continueOnError": false,
      "description": "Click plus operator"
    },
    {
      "order": 3,
      "type": "Click",
      "target": "Button[@Name='2']", 
      "value": null,
      "timeoutMs": 5000,
      "continueOnError": false,
      "description": "Click number 2"
    },
    {
      "order": 4,
      "type": "Click",
      "target": "Button[@Name='=']",
      "value": null,
      "timeoutMs": 5000,
      "continueOnError": false,
      "description": "Click equals"
    },
    {
      "order": 5,
      "type": "Validate",
      "target": "Text[@Name='Display']",
      "value": "3",
      "timeoutMs": 5000,
      "continueOnError": false,
      "description": "Validate result is 3"
    }
  ],
  "priority": "Normal",
  "webhookUrl": "https://your-webhook.com/rpa-notifications"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "987fcdeb-51a2-43d7-8f9e-123456789abc",
    "name": "Calculator Test",
    "applicationPath": "calc.exe",
    "arguments": null,
    "steps": [...],
    "status": "Queued",
    "priority": "Normal",
    "createdAt": "2025-11-18T10:05:00Z",
    "startedAt": null,
    "completedAt": null,
    "assignedAgentId": null,
    "result": null,
    "errorMessage": null,
    "screenshots": [],
    "duration": null
  }
}
```

#### Get Job by ID
```http
GET /api/automation/jobs/{id}
```

#### Get Jobs (with filtering)
```http
GET /api/automation/jobs?status=Success&skip=0&take=20
```

#### Update Job Status
```http
PATCH /api/automation/jobs/{id}/status
```

**Request Body:**
```json
{
  "status": "Success",
  "result": "Calculator test completed successfully. Result: 3",
  "errorMessage": null
}
```

#### Add Screenshot to Job
```http
POST /api/automation/jobs/{id}/screenshots
```

**Request Body:**
```json
{
  "screenshotPath": "/screenshots/job-123-step-5.png"
}
```

#### Cancel Job
```http
POST /api/automation/jobs/{id}/cancel
```

## Job Status Flow
1. **Pending** → Job created but not yet queued
2. **Queued** → Job added to queue, waiting for agent
3. **Assigned** → Job assigned to specific agent
4. **Running** → Agent is executing the job
5. **Success** → Job completed successfully
6. **Failed** → Job failed with error
7. **Cancelled** → Job was cancelled by user
8. **Timeout** → Job exceeded maximum execution time

## Agent Status Flow
1. **Starting** → Agent is being created and session initialized
2. **Idle** → Agent is ready and waiting for jobs
3. **Busy** → Agent is executing a job
4. **Error** → Agent encountered an error
5. **Offline** → Agent is not responding
6. **Terminating** → Agent is being shut down

## Step Types
- **Click**: Click on UI element
- **Type**: Enter text into input field
- **Wait**: Delay execution for specified time
- **Validate**: Assert condition or value
- **Screenshot**: Capture screen image
- **GetText**: Extract text from element
- **SetFocus**: Set focus to element
- **KeyPress**: Send keyboard input

## Error Handling
All endpoints return consistent error responses:

```json
{
  "success": false,
  "data": null,
  "errorMessage": "Agent not found",
  "errors": []
}
```

## Example Usage

### Complete Workflow Example

1. **Create an Agent:**
```bash
curl -X POST http://localhost:5000/api/agents \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Calculator-Agent",
    "windowsUser": "AutoAgent01"
  }'
```

2. **Wait for Agent to be Ready (Status: Idle)**

3. **Create a Job:**
```bash
curl -X POST http://localhost:5000/api/automation/jobs \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Basic Calculator Test",
    "applicationPath": "calc.exe",
    "steps": [
      {
        "order": 1,
        "type": "Click",
        "target": "Button[@Name=\"1\"]",
        "description": "Click 1"
      },
      {
        "order": 2, 
        "type": "Click",
        "target": "Button[@Name=\"+\"]",
        "description": "Click plus"
      },
      {
        "order": 3,
        "type": "Click", 
        "target": "Button[@Name=\"1\"]",
        "description": "Click 1 again"
      },
      {
        "order": 4,
        "type": "Click",
        "target": "Button[@Name=\"=\"]", 
        "description": "Click equals"
      }
    ]
  }'
```

4. **Monitor Job Status:**
```bash
curl http://localhost:5000/api/automation/jobs/{job-id}
```

The job will automatically be assigned to an available agent and executed according to the automation steps defined.