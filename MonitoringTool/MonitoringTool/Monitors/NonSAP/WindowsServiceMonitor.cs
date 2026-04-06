using MonitoringTool.Models;
using MonitoringTool.Monitors;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace MonitoringTool.Monitors.NonSAP;

/// <summary>
/// Monitors Windows service status (Running / Stopped / etc.).
/// On non-Windows platforms the check is skipped gracefully.
/// </summary>
public class WindowsServiceMonitor : BaseMonitor
{
    private readonly WindowsServiceConfig _config;

    public WindowsServiceMonitor(WindowsServiceConfig config, ILogger<WindowsServiceMonitor> logger)
        : base(logger)
    {
        _config = config;
    }

    public override string SystemName => _config.Name;
    public override string Category => "Windows Service";
    public override bool IsEnabled => _config.Enabled;

    protected override Task<MonitoringResult> ExecuteCheckAsync(CancellationToken ct)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult(new MonitoringResult
            {
                SystemName = SystemName,
                Category = Category,
                Status = HealthStatus.Unknown,
                Message = "Windows Service monitoring is only available on Windows.",
                CheckedAt = DateTime.UtcNow
            });
        }

        return Task.FromResult(CheckService());
    }

    private MonitoringResult CheckService()
    {
        try
        {
            using var sc = string.IsNullOrEmpty(_config.MachineName)
                ? new System.ServiceProcess.ServiceController(_config.ServiceName)
                : new System.ServiceProcess.ServiceController(_config.ServiceName, _config.MachineName);

            var status = sc.Status;
            var result = new MonitoringResult
            {
                SystemName = SystemName,
                Category = Category,
                CheckedAt = DateTime.UtcNow
            };

            result.Metrics["ServiceStatus"] = status.ToString();

            switch (status)
            {
                case System.ServiceProcess.ServiceControllerStatus.Running:
                    result.Status = HealthStatus.Healthy;
                    result.Message = $"Service '{_config.ServiceName}' is Running.";
                    break;

                case System.ServiceProcess.ServiceControllerStatus.StartPending:
                case System.ServiceProcess.ServiceControllerStatus.StopPending:
                    result.Status = HealthStatus.Degraded;
                    result.Message = $"Service '{_config.ServiceName}' is in a transitional state: {status}.";
                    break;

                default:
                    result.Status = HealthStatus.Unhealthy;
                    result.Message = $"Service '{_config.ServiceName}' is not running: {status}.";
                    break;
            }

            return result;
        }
        catch (Exception ex)
        {
            return MonitoringResult.Unhealthy(SystemName, Category,
                $"Cannot query service '{_config.ServiceName}': {ex.Message}", ex);
        }
    }
}
