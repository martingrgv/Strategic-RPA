using RPA.Orchestrator;
using RPA.Orchestrator.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure background worker services
builder.Services.AddHostedService<Worker>();

// Add orchestrator services
builder.Services.AddSingleton<ISessionManager, SessionManager>();
builder.Services.AddSingleton<IAgentManager, AgentManager>();
builder.Services.AddSingleton<IJobScheduler, JobScheduler>();
builder.Services.AddSingleton<IHealthMonitor, HealthMonitor>();
builder.Services.AddSingleton<IAgentCommunicationService, AgentCommunicationService>();
builder.Services.AddHttpClient<IAgentCommunicationService, AgentCommunicationService>();

// Configure logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add configuration for appsettings
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);
builder.Configuration.AddEnvironmentVariables();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseHttpsRedirection();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "RPA.Orchestrator", Timestamp = DateTime.UtcNow }))
   .WithName("OrchestratorHealthCheck");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Strategic RPA Orchestrator (API + Worker) starting on port 5001...");

app.Run("http://localhost:5001");
