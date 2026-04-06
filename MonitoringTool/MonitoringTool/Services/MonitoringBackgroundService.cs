using Microsoft.Extensions.Hosting;

namespace MonitoringTool.Services;

/// <summary>
/// Wraps MonitoringOrchestrator as an IHostedService so it runs
/// in the background alongside the ASP.NET Core Web API.
/// </summary>
public class MonitoringBackgroundService : BackgroundService
{
    private readonly MonitoringOrchestrator _orchestrator;

    public MonitoringBackgroundService(MonitoringOrchestrator orchestrator)
        => _orchestrator = orchestrator;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _orchestrator.RunAsync(stoppingToken);
}
