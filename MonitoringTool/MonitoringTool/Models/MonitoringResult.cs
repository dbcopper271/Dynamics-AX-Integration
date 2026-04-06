namespace MonitoringTool.Models;

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}

/// <summary>
/// Result of a single health check from any monitor.
/// </summary>
public class MonitoringResult
{
    public string SystemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public HealthStatus Status { get; set; } = HealthStatus.Unknown;
    public string Message { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan ResponseTime { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
    public string? ErrorDetails { get; set; }

    public static MonitoringResult Healthy(string systemName, string category, string message, TimeSpan responseTime)
        => new()
        {
            SystemName = systemName,
            Category = category,
            Status = HealthStatus.Healthy,
            Message = message,
            ResponseTime = responseTime,
            CheckedAt = DateTime.UtcNow
        };

    public static MonitoringResult Degraded(string systemName, string category, string message, TimeSpan responseTime)
        => new()
        {
            SystemName = systemName,
            Category = category,
            Status = HealthStatus.Degraded,
            Message = message,
            ResponseTime = responseTime,
            CheckedAt = DateTime.UtcNow
        };

    public static MonitoringResult Unhealthy(string systemName, string category, string message, Exception? ex = null)
        => new()
        {
            SystemName = systemName,
            Category = category,
            Status = HealthStatus.Unhealthy,
            Message = message,
            ErrorDetails = ex?.ToString(),
            CheckedAt = DateTime.UtcNow
        };
}
