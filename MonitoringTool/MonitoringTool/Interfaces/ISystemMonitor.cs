using MonitoringTool.Models;

namespace MonitoringTool.Interfaces;

/// <summary>
/// Common interface for all system monitors (SAP and Non-SAP).
/// </summary>
public interface ISystemMonitor
{
    /// <summary>Unique name of the system being monitored.</summary>
    string SystemName { get; }

    /// <summary>Category: SAP, Database, WebService, FileSystem, etc.</summary>
    string Category { get; }

    /// <summary>Execute a health check and return the result.</summary>
    Task<MonitoringResult> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether this monitor is currently enabled.</summary>
    bool IsEnabled { get; }
}
