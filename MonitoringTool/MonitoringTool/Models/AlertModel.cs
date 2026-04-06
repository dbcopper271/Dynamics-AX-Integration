namespace MonitoringTool.Models;

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Represents an alert generated when a system's health degrades.
/// </summary>
public class AlertModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SystemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public HealthStatus CurrentStatus { get; set; }
    public HealthStatus PreviousStatus { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public string? ErrorDetails { get; set; }
}
