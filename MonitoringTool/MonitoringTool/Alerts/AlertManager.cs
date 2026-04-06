using MonitoringTool.Interfaces;
using MonitoringTool.Models;
using Microsoft.Extensions.Logging;

namespace MonitoringTool.Alerts;

/// <summary>
/// Tracks previous health states and fires alerts whenever a system transitions
/// to a worse state (Healthy → Degraded, Healthy/Degraded → Unhealthy).
/// Recovery alerts are sent when a system returns to Healthy.
/// </summary>
public class AlertManager
{
    private readonly IReadOnlyList<IAlertProvider> _providers;
    private readonly ILogger<AlertManager> _logger;
    private readonly Dictionary<string, HealthStatus> _previousStates = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AlertManager(IEnumerable<IAlertProvider> providers, ILogger<AlertManager> logger)
    {
        _providers = providers.ToList();
        _logger = logger;
    }

    /// <summary>
    /// Evaluate a new monitoring result and send alerts if the state changed.
    /// </summary>
    public async Task EvaluateAsync(MonitoringResult result, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var key = result.SystemName;
            _previousStates.TryGetValue(key, out var previous);
            _previousStates[key] = result.Status;

            if (previous == result.Status) return;  // no state change

            // Determine alert severity
            AlertSeverity severity = result.Status switch
            {
                HealthStatus.Unhealthy => AlertSeverity.Critical,
                HealthStatus.Degraded  => AlertSeverity.Warning,
                HealthStatus.Healthy   => AlertSeverity.Info,     // recovery
                _                      => AlertSeverity.Info
            };

            var alert = new AlertModel
            {
                SystemName      = result.SystemName,
                Category        = result.Category,
                Severity        = severity,
                CurrentStatus   = result.Status,
                PreviousStatus  = previous,
                Message         = result.Message,
                TriggeredAt     = result.CheckedAt,
                ErrorDetails    = result.ErrorDetails
            };

            _logger.LogInformation(
                "State change: [{System}] {Prev} → {Current}",
                result.SystemName, previous, result.Status);

            await DispatchAsync(alert, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task DispatchAsync(AlertModel alert, CancellationToken ct)
    {
        var tasks = _providers.Select(p => SendSafeAsync(p, alert, ct));
        await Task.WhenAll(tasks);
    }

    private async Task SendSafeAsync(IAlertProvider provider, AlertModel alert, CancellationToken ct)
    {
        try
        {
            await provider.SendAlertAsync(alert, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Alert provider '{Provider}' failed to send alert for {System}.",
                provider.ProviderName, alert.SystemName);
        }
    }
}
