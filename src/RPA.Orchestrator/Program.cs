using RPA.Orchestrator;
using RPA.Orchestrator.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure services
builder.Services.AddHostedService<Worker>();

// Add orchestrator services
builder.Services.AddSingleton<ISessionManager, SessionManager>();
builder.Services.AddSingleton<IAgentManager, AgentManager>();
builder.Services.AddSingleton<IJobScheduler, JobScheduler>();
builder.Services.AddSingleton<IHealthMonitor, HealthMonitor>();

// Configure logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add configuration for appsettings
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);
builder.Configuration.AddEnvironmentVariables();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Strategic RPA Orchestrator initializing...");

await host.RunAsync();
