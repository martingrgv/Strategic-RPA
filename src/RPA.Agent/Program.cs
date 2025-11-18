using RPA.Agent;
using RPA.Agent.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure background worker services
builder.Services.AddHostedService<Worker>();

// Add automation services
builder.Services.AddSingleton<IAutomationEngine, FlaUIAutomationEngine>();
builder.Services.AddSingleton<IJobExecutor, JobExecutor>();
builder.Services.AddScoped<IApiReportingService, ApiReportingService>();
builder.Services.AddHttpClient<IApiReportingService, ApiReportingService>();

// Configure logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add configuration
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
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "RPA.Agent", Timestamp = DateTime.UtcNow }))
   .WithName("AgentHealthCheck");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Strategic RPA Agent (API + Worker) starting on port 8080...");

app.Run("http://localhost:8080");
