using MonitoringTool.Interfaces;
using MonitoringTool.Models;
using Microsoft.Extensions.Logging;

namespace MonitoringTool.Monitors;

/// <summary>
/// Abstract base class providing common scaffolding for all monitors.
/// </summary>
public abstract class BaseMonitor : ISystemMonitor
{
    protected readonly ILogger Logger;

    protected BaseMonitor(ILogger logger)
    {
        Logger = logger;
    }

    public abstract string SystemName { get; }
    public abstract string Category { get; }
    public abstract bool IsEnabled { get; }

    public async Task<MonitoringResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return new MonitoringResult
            {
                SystemName = SystemName,
                Category = Category,
                Status = HealthStatus.Unknown,
                Message = "Monitor is disabled.",
                CheckedAt = DateTime.UtcNow
            };
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await ExecuteCheckAsync(cancellationToken);
            sw.Stop();
            result.ResponseTime = sw.Elapsed;
            return result;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            Logger.LogWarning("Health check for {System} was cancelled.", SystemName);
            return MonitoringResult.Unhealthy(SystemName, Category, "Check was cancelled or timed out.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Logger.LogError(ex, "Unhandled exception during health check for {System}.", SystemName);
            return MonitoringResult.Unhealthy(SystemName, Category, $"Unexpected error: {ex.Message}", ex);
        }
    }

    /// <summary>Template method — implement the actual check logic here.</summary>
    protected abstract Task<MonitoringResult> ExecuteCheckAsync(CancellationToken cancellationToken);
}
