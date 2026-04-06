using MonitoringTool.Alerts;
using MonitoringTool.Interfaces;
using MonitoringTool.Models;
using Microsoft.Extensions.Logging;

namespace MonitoringTool.Services;

/// <summary>
/// Runs all registered monitors on a configurable polling interval,
/// dispatches results to the alert manager, and maintains an in-memory
/// results cache for the dashboard.
/// </summary>
public class MonitoringOrchestrator
{
    private readonly IReadOnlyList<ISystemMonitor> _monitors;
    private readonly AlertManager _alertManager;
    private readonly ILogger<MonitoringOrchestrator> _logger;
    private readonly int _pollingIntervalSeconds;
    private readonly int _timeoutSeconds;

    // In-memory results cache: systemName → latest result
    private readonly Dictionary<string, MonitoringResult> _results = new();
    private readonly SemaphoreSlim _resultsLock = new(1, 1);

    public event Action<MonitoringResult>? ResultUpdated;

    public MonitoringOrchestrator(
        IEnumerable<ISystemMonitor> monitors,
        AlertManager alertManager,
        ILogger<MonitoringOrchestrator> logger,
        int pollingIntervalSeconds = 60,
        int timeoutSeconds = 30)
    {
        _monitors               = monitors.ToList();
        _alertManager           = alertManager;
        _logger                 = logger;
        _pollingIntervalSeconds = pollingIntervalSeconds;
        _timeoutSeconds         = timeoutSeconds;
    }

    /// <summary>
    /// Starts the monitoring loop. Runs until the cancellation token is triggered.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("Monitoring started — {Count} monitor(s), interval {Interval}s",
            _monitors.Count, _pollingIntervalSeconds);

        // First run immediately, then on interval
        await RunAllChecksAsync(ct);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_pollingIntervalSeconds));
        while (await timer.WaitForNextTickAsync(ct))
        {
            await RunAllChecksAsync(ct);
        }
    }

    /// <summary>Returns a snapshot of all latest results.</summary>
    public async Task<IReadOnlyList<MonitoringResult>> GetLatestResultsAsync()
    {
        await _resultsLock.WaitAsync();
        try
        {
            return _results.Values.ToList();
        }
        finally
        {
            _resultsLock.Release();
        }
    }

    private async Task RunAllChecksAsync(CancellationToken ct)
    {
        _logger.LogDebug("Running health checks for {Count} monitor(s)...", _monitors.Count);

        var tasks = _monitors.Select(m => RunSingleCheckAsync(m, ct));
        await Task.WhenAll(tasks);
    }

    private async Task RunSingleCheckAsync(ISystemMonitor monitor, CancellationToken parentCt)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentCt);
        cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        MonitoringResult result;
        try
        {
            result = await monitor.CheckHealthAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!parentCt.IsCancellationRequested)
        {
            result = MonitoringResult.Unhealthy(
                monitor.SystemName, monitor.Category,
                $"Health check timed out after {_timeoutSeconds}s");
        }

        // Update cache
        await _resultsLock.WaitAsync(parentCt);
        try { _results[monitor.SystemName] = result; }
        finally { _resultsLock.Release(); }

        // Fire event for dashboard refresh
        ResultUpdated?.Invoke(result);

        // Evaluate for alerts (state change detection)
        await _alertManager.EvaluateAsync(result, parentCt);

        LogResult(result);
    }

    private void LogResult(MonitoringResult r)
    {
        var level = r.Status switch
        {
            HealthStatus.Healthy   => Microsoft.Extensions.Logging.LogLevel.Debug,
            HealthStatus.Degraded  => Microsoft.Extensions.Logging.LogLevel.Warning,
            HealthStatus.Unhealthy => Microsoft.Extensions.Logging.LogLevel.Error,
            _                      => Microsoft.Extensions.Logging.LogLevel.Information
        };
        _logger.Log(level, "[{Status}] {System} ({Category}): {Message}",
            r.Status, r.SystemName, r.Category, r.Message);
    }
}
