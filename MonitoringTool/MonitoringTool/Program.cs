using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MonitoringTool.Alerts;
using MonitoringTool.Dashboard;
using MonitoringTool.Interfaces;
using MonitoringTool.Models;
using MonitoringTool.Monitors.NonSAP;
using MonitoringTool.Monitors.SAP;
using MonitoringTool.Services;

// ─────────────────────────────────────────────────────────────────────────────
// Determine run mode
//   --api       → run as ASP.NET Core Web API (serves dashboard UI)
//   (default)   → run as console monitoring tool
// ─────────────────────────────────────────────────────────────────────────────
bool runAsApi = args.Contains("--api");

// ─────────────────────────────────────────────────────────────────────────────
// Build configuration
// ─────────────────────────────────────────────────────────────────────────────
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("Config/appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables("MONITORING_")
    .Build();

var monCfg = configuration
    .GetSection("MonitoringConfig")
    .Get<MonitoringConfig>() ?? new MonitoringConfig();

// ─────────────────────────────────────────────────────────────────────────────
// Shared service registration
// ─────────────────────────────────────────────────────────────────────────────
void RegisterMonitoringServices(IServiceCollection services)
{
    services.AddSingleton(monCfg);
    services.AddSingleton<AlertHistory>();

    // Alert providers
    if (monCfg.Alerts.EnableConsoleAlerts)
        services.AddSingleton<IAlertProvider, ConsoleAlertProvider>();
    if (monCfg.Alerts.EnableFileLog)
        services.AddSingleton<IAlertProvider>(
            _ => new FileLogAlertProvider(monCfg.Alerts.LogFilePath));
    if (monCfg.Alerts.Email.Enabled)
        services.AddSingleton<IAlertProvider>(sp =>
            new EmailAlertProvider(monCfg.Alerts.Email,
                sp.GetRequiredService<ILogger<EmailAlertProvider>>()));
    if (monCfg.Alerts.Webhook.Enabled)
        services.AddSingleton<IAlertProvider>(sp =>
            new WebhookAlertProvider(monCfg.Alerts.Webhook,
                sp.GetRequiredService<ILogger<WebhookAlertProvider>>()));

    services.AddSingleton<AlertManager>();

    // SAP monitors
    foreach (var sapCfg in monCfg.SapSystems)
        services.AddSingleton<ISystemMonitor>(sp =>
            new SapSystemMonitor(sapCfg, sp.GetRequiredService<ILogger<SapSystemMonitor>>()));

    // Non-SAP monitors
    foreach (var axCfg in monCfg.DynamicsAxSystems)
        services.AddSingleton<ISystemMonitor>(sp =>
            new DynamicsAxMonitor(axCfg, sp.GetRequiredService<ILogger<DynamicsAxMonitor>>()));
    foreach (var dbCfg in monCfg.Databases)
        services.AddSingleton<ISystemMonitor>(sp =>
            new DatabaseMonitor(dbCfg, sp.GetRequiredService<ILogger<DatabaseMonitor>>()));
    foreach (var apiCfg in monCfg.WebApis)
        services.AddSingleton<ISystemMonitor>(sp =>
            new WebApiMonitor(apiCfg, sp.GetRequiredService<ILogger<WebApiMonitor>>()));
    foreach (var svcCfg in monCfg.WindowsServices)
        services.AddSingleton<ISystemMonitor>(sp =>
            new WindowsServiceMonitor(svcCfg, sp.GetRequiredService<ILogger<WindowsServiceMonitor>>()));
    foreach (var fsCfg in monCfg.FileSystems)
        services.AddSingleton<ISystemMonitor>(sp =>
            new FileSystemMonitor(fsCfg, sp.GetRequiredService<ILogger<FileSystemMonitor>>()));

    services.AddSingleton<MonitoringOrchestrator>(sp =>
        new MonitoringOrchestrator(
            sp.GetServices<ISystemMonitor>(),
            sp.GetRequiredService<AlertManager>(),
            sp.GetRequiredService<ILogger<MonitoringOrchestrator>>(),
            monCfg.PollingIntervalSeconds,
            monCfg.TimeoutSeconds));
}

// ─────────────────────────────────────────────────────────────────────────────
// Web API mode (dotnet run -- --api)
// ─────────────────────────────────────────────────────────────────────────────
if (runAsApi)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

    RegisterMonitoringServices(builder.Services);

    // Background monitoring service
    builder.Services.AddHostedService<MonitoringBackgroundService>();

    var app = builder.Build();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors();
    app.UseStaticFiles();       // serves MonitoringUI build from wwwroot/
    app.MapControllers();
    app.MapFallbackToFile("index.html");  // SPA fallback

    app.Run();
    return;
}

// ─────────────────────────────────────────────────────────────────────────────
// Console mode (default)
// ─────────────────────────────────────────────────────────────────────────────
var services2 = new ServiceCollection();
services2.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
RegisterMonitoringServices(services2);
services2.AddSingleton<ConsoleDashboard>();

var sp2 = services2.BuildServiceProvider();
var orchestrator = sp2.GetRequiredService<MonitoringOrchestrator>();
var dashboard    = sp2.GetRequiredService<ConsoleDashboard>();
var logger       = sp2.GetRequiredService<ILogger<Program>>();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

dashboard.Start();
try   { await orchestrator.RunAsync(cts.Token); }
catch (OperationCanceledException) { }
finally { dashboard.Stop(); }
logger.LogInformation("IT Monitoring Tool stopped.");
