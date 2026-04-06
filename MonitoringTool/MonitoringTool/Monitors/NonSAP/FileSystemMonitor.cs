using MonitoringTool.Models;
using MonitoringTool.Monitors;
using Microsoft.Extensions.Logging;

namespace MonitoringTool.Monitors.NonSAP;

/// <summary>
/// Monitors file system paths for:
///  - Directory accessibility
///  - Disk free space thresholds
///  - Stale file detection (no new files within a configurable window)
/// </summary>
public class FileSystemMonitor : BaseMonitor
{
    private readonly FileSystemConfig _config;

    public FileSystemMonitor(FileSystemConfig config, ILogger<FileSystemMonitor> logger)
        : base(logger)
    {
        _config = config;
    }

    public override string SystemName => _config.Name;
    public override string Category => "File System";
    public override bool IsEnabled => _config.Enabled;

    protected override Task<MonitoringResult> ExecuteCheckAsync(CancellationToken ct)
    {
        var result = new MonitoringResult
        {
            SystemName = SystemName,
            Category = Category,
            CheckedAt = DateTime.UtcNow
        };

        // 1. Accessibility check
        if (!Directory.Exists(_config.Path))
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = $"Path not accessible or does not exist: {_config.Path}";
            return Task.FromResult(result);
        }

        var issues = new List<string>();
        HealthStatus status = HealthStatus.Healthy;

        // 2. Disk free space
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(_config.Path) ?? _config.Path);
            long freeMB = drive.AvailableFreeSpace / (1024 * 1024);
            result.Metrics["FreeSpaceMB"] = freeMB;
            result.Metrics["TotalSpaceMB"] = drive.TotalSize / (1024 * 1024);

            if (freeMB <= _config.FreeSpaceCriticalMB)
            {
                issues.Add($"Critical: only {freeMB} MB free (threshold: {_config.FreeSpaceCriticalMB} MB)");
                status = HealthStatus.Unhealthy;
            }
            else if (freeMB <= _config.FreeSpaceWarningMB)
            {
                issues.Add($"Warning: {freeMB} MB free (threshold: {_config.FreeSpaceWarningMB} MB)");
                if (status < HealthStatus.Degraded) status = HealthStatus.Degraded;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not read disk free space for {Path}", _config.Path);
        }

        // 3. Stale file detection
        if (_config.StaleFileWindowMinutes.HasValue)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-_config.StaleFileWindowMinutes.Value);
                var latestFile = Directory
                    .EnumerateFiles(_config.Path, "*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();

                if (latestFile == null)
                {
                    issues.Add($"No files found in {_config.Path}");
                    if (status < HealthStatus.Degraded) status = HealthStatus.Degraded;
                }
                else
                {
                    result.Metrics["LatestFileAgeMinutes"] = (int)(DateTime.UtcNow - latestFile.LastWriteTimeUtc).TotalMinutes;
                    if (latestFile.LastWriteTimeUtc < cutoff)
                    {
                        issues.Add($"No new files in {_config.StaleFileWindowMinutes} min (last: {latestFile.Name})");
                        if (status < HealthStatus.Degraded) status = HealthStatus.Degraded;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Stale file check failed for {Path}", _config.Path);
            }
        }

        result.Status = status;
        result.Message = issues.Count > 0
            ? string.Join("; ", issues)
            : $"File system healthy: {_config.Path}";

        return Task.FromResult(result);
    }
}
