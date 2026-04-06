using MonitoringTool.Interfaces;
using MonitoringTool.Models;

namespace MonitoringTool.Alerts;

/// <summary>
/// Prints colorized alert messages to the console.
/// </summary>
public class ConsoleAlertProvider : IAlertProvider
{
    public string ProviderName => "Console";

    public Task SendAlertAsync(AlertModel alert, CancellationToken ct = default)
    {
        var (fg, label) = alert.Severity switch
        {
            AlertSeverity.Critical => (ConsoleColor.Red,     "[CRITICAL]"),
            AlertSeverity.Warning  => (ConsoleColor.Yellow,  "[WARNING] "),
            _                      => (ConsoleColor.Green,   "[RECOVERY]")
        };

        var prev = Console.ForegroundColor;
        Console.ForegroundColor = fg;
        Console.WriteLine(
            $"{alert.TriggeredAt:yyyy-MM-dd HH:mm:ss} {label} [{alert.Category}] {alert.SystemName} " +
            $"| {alert.PreviousStatus} → {alert.CurrentStatus} | {alert.Message}");
        Console.ForegroundColor = prev;

        return Task.CompletedTask;
    }
}
