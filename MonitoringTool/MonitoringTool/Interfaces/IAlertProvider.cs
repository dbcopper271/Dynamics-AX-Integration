using MonitoringTool.Models;

namespace MonitoringTool.Interfaces;

/// <summary>
/// Interface for alert delivery providers (Email, SMS, Log, Webhook, etc.).
/// </summary>
public interface IAlertProvider
{
    string ProviderName { get; }

    Task SendAlertAsync(AlertModel alert, CancellationToken cancellationToken = default);
}
