using MonitoringTool.Interfaces;
using MonitoringTool.Models;

namespace MonitoringTool.Alerts;

/// <summary>
/// Appends alert records to a rolling log file (one line per alert, JSON-formatted).
/// </summary>
public class FileLogAlertProvider : IAlertProvider
{
    private readonly string _logFilePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public FileLogAlertProvider(string logFilePath)
    {
        _logFilePath = logFilePath;
        var dir = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public string ProviderName => "FileLog";

    public async Task SendAlertAsync(AlertModel alert, CancellationToken ct = default)
    {
        var line = System.Text.Json.JsonSerializer.Serialize(new
        {
            alert.TriggeredAt,
            alert.Severity,
            alert.Category,
            alert.SystemName,
            Previous  = alert.PreviousStatus.ToString(),
            Current   = alert.CurrentStatus.ToString(),
            alert.Message,
            alert.ErrorDetails
        });

        await _writeLock.WaitAsync(ct);
        try
        {
            await File.AppendAllTextAsync(_logFilePath, line + Environment.NewLine, ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
