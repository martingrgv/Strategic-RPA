using RPA.Agent;
using RPA.Agent.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure services
builder.Services.AddHostedService<Worker>();

// Add automation services
builder.Services.AddSingleton<IAutomationEngine, FlaUIAutomationEngine>();
builder.Services.AddSingleton<IJobExecutor, JobExecutor>();

// Configure logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);
builder.Configuration.AddEnvironmentVariables();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("RPA Agent initializing...");

await host.RunAsync();
