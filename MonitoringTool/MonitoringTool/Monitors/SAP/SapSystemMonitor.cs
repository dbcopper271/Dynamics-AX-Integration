using MonitoringTool.Models;
using MonitoringTool.Monitors;
using Microsoft.Extensions.Logging;

namespace MonitoringTool.Monitors.SAP;

/// <summary>
/// Monitors SAP system health: availability, response time, work processes,
/// ABAP short dumps, and transport queue depth.
///
/// Production usage: replace the simulated RFC calls with real SAP NCo 3.x
/// (SAP .NET Connector) calls:
///   RfcDestination dest = RfcDestinationManager.GetDestination(config.Name);
///   IRfcFunction fn = dest.Repository.CreateFunction("RFC_SYSTEM_INFO");
///   fn.Invoke(dest);
/// </summary>
public class SapSystemMonitor : BaseMonitor
{
    private readonly SapSystemConfig _config;
    private readonly SapRfcClient _rfcClient;

    public SapSystemMonitor(SapSystemConfig config, ILogger<SapSystemMonitor> logger)
        : base(logger)
    {
        _config = config;
        _rfcClient = new SapRfcClient(config, logger);
    }

    public override string SystemName => _config.Name;
    public override string Category => "SAP";
    public override bool IsEnabled => _config.Enabled;

    protected override async Task<MonitoringResult> ExecuteCheckAsync(CancellationToken cancellationToken)
    {
        var result = new MonitoringResult
        {
            SystemName = SystemName,
            Category = Category,
            CheckedAt = DateTime.UtcNow
        };

        // 1. Ping / login check
        var (connected, loginMs, loginError) = await _rfcClient.PingAsync(cancellationToken);
        if (!connected)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = $"SAP system unreachable: {loginError}";
            result.ErrorDetails = loginError;
            return result;
        }

        result.Metrics["LoginResponseMs"] = loginMs;

        // 2. Evaluate response time thresholds
        HealthStatus status = loginMs >= _config.ResponseTimeCriticalMs ? HealthStatus.Unhealthy
                            : loginMs >= _config.ResponseTimeWarningMs  ? HealthStatus.Degraded
                            : HealthStatus.Healthy;

        var issues = new List<string>();

        // 3. Work process availability
        if (_config.MonitorWorkProcesses)
        {
            var (freePercent, wpError) = await _rfcClient.GetFreeWorkProcessPercentAsync(cancellationToken);
            if (wpError != null)
            {
                issues.Add($"Work process check failed: {wpError}");
                if (status < HealthStatus.Degraded) status = HealthStatus.Degraded;
            }
            else
            {
                result.Metrics["FreeWorkProcessPercent"] = freePercent;
                if (freePercent < _config.MinFreeWorkProcessPercent)
                {
                    issues.Add($"Free work processes {freePercent:F0}% below threshold {_config.MinFreeWorkProcessPercent}%");
                    if (status < HealthStatus.Degraded) status = HealthStatus.Degraded;
                }
            }
        }

        // 4. ABAP short dumps
        if (_config.MonitorShortDumps)
        {
            var (dumpCount, dumpError) = await _rfcClient.GetShortDumpCountAsync(
                _config.ShortDumpWindowMinutes, cancellationToken);

            if (dumpError != null)
            {
                issues.Add($"Short dump check failed: {dumpError}");
            }
            else
            {
                result.Metrics["ShortDumpCount"] = dumpCount;
                if (dumpCount >= _config.ShortDumpWarningThreshold)
                {
                    issues.Add($"{dumpCount} ABAP short dumps in last {_config.ShortDumpWindowMinutes} min");
                    if (status < HealthStatus.Degraded) status = HealthStatus.Degraded;
                }
            }
        }

        // 5. Transport queue depth
        if (_config.MonitorTransports)
        {
            var (queueDepth, tpError) = await _rfcClient.GetTransportQueueDepthAsync(cancellationToken);
            if (tpError != null)
            {
                issues.Add($"Transport check failed: {tpError}");
            }
            else
            {
                result.Metrics["TransportQueueDepth"] = queueDepth;
                if (queueDepth >= _config.TransportQueueWarningThreshold)
                {
                    issues.Add($"Transport queue depth {queueDepth} exceeds threshold {_config.TransportQueueWarningThreshold}");
                    if (status < HealthStatus.Degraded) status = HealthStatus.Degraded;
                }
            }
        }

        result.Status = status;
        result.Message = issues.Count > 0
            ? string.Join("; ", issues)
            : $"SAP system healthy. Login: {loginMs}ms, SID: {_config.SystemId}";

        return result;
    }
}
