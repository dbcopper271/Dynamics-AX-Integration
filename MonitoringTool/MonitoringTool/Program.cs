using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonitoringTool.Alerts;
using MonitoringTool.Dashboard;
using MonitoringTool.Interfaces;
using MonitoringTool.Models;
using MonitoringTool.Monitors.NonSAP;
using MonitoringTool.Monitors.SAP;
using MonitoringTool.Services;

// ─────────────────────────────────────────────────────────────────────────────
// Build configuration
// ─────────────────────────────────────────────────────────────────────────────

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("Config/appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables("MONITORING_")   // override any setting via env vars
    .Build();

var monCfg = configuration
    .GetSection("MonitoringConfig")
    .Get<MonitoringConfig>() ?? new MonitoringConfig();

// ─────────────────────────────────────────────────────────────────────────────
// Build DI container
// ─────────────────────────────────────────────────────────────────────────────

var services = new ServiceCollection();

services.AddLogging(b => b
    .AddConsole()
    .SetMinimumLevel(LogLevel.Information));

services.AddSingleton(monCfg);

// ── Register alert providers ─────────────────────────────────────────────────

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

// ── Register SAP monitors ────────────────────────────────────────────────────

foreach (var sapCfg in monCfg.SapSystems)
{
    services.AddSingleton<ISystemMonitor>(sp =>
        new SapSystemMonitor(sapCfg,
            sp.GetRequiredService<ILogger<SapSystemMonitor>>()));
}

// ── Register Non-SAP monitors ────────────────────────────────────────────────

foreach (var axCfg in monCfg.DynamicsAxSystems)
{
    services.AddSingleton<ISystemMonitor>(sp =>
        new DynamicsAxMonitor(axCfg,
            sp.GetRequiredService<ILogger<DynamicsAxMonitor>>()));
}

foreach (var dbCfg in monCfg.Databases)
{
    services.AddSingleton<ISystemMonitor>(sp =>
        new DatabaseMonitor(dbCfg,
            sp.GetRequiredService<ILogger<DatabaseMonitor>>()));
}

foreach (var apiCfg in monCfg.WebApis)
{
    services.AddSingleton<ISystemMonitor>(sp =>
        new WebApiMonitor(apiCfg,
            sp.GetRequiredService<ILogger<WebApiMonitor>>()));
}

foreach (var svcCfg in monCfg.WindowsServices)
{
    services.AddSingleton<ISystemMonitor>(sp =>
        new WindowsServiceMonitor(svcCfg,
            sp.GetRequiredService<ILogger<WindowsServiceMonitor>>()));
}

foreach (var fsCfg in monCfg.FileSystems)
{
    services.AddSingleton<ISystemMonitor>(sp =>
        new FileSystemMonitor(fsCfg,
            sp.GetRequiredService<ILogger<FileSystemMonitor>>()));
}

// ── Orchestrator & Dashboard ─────────────────────────────────────────────────

services.AddSingleton<MonitoringOrchestrator>(sp =>
    new MonitoringOrchestrator(
        sp.GetServices<ISystemMonitor>(),
        sp.GetRequiredService<AlertManager>(),
        sp.GetRequiredService<ILogger<MonitoringOrchestrator>>(),
        monCfg.PollingIntervalSeconds,
        monCfg.TimeoutSeconds));

services.AddSingleton<ConsoleDashboard>();

// ─────────────────────────────────────────────────────────────────────────────
// Run
// ─────────────────────────────────────────────────────────────────────────────

var sp = services.BuildServiceProvider();
var orchestrator = sp.GetRequiredService<MonitoringOrchestrator>();
var dashboard    = sp.GetRequiredService<ConsoleDashboard>();
var logger       = sp.GetRequiredService<ILogger<Program>>();

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    logger.LogInformation("Shutdown requested...");
    cts.Cancel();
};

dashboard.Start();

try
{
    await orchestrator.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    // Normal shutdown
}
finally
{
    dashboard.Stop();
}

logger.LogInformation("IT Monitoring Tool stopped.");
