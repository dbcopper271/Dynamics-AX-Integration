using System.Text;
using System.Text.Json;
using MonitoringTool.Interfaces;
using MonitoringTool.Models;
using Microsoft.Extensions.Logging;

namespace MonitoringTool.Alerts;

/// <summary>
/// Posts alert payloads to a webhook URL (e.g. Microsoft Teams, Slack, or
/// any custom HTTP endpoint).
///
/// Teams Adaptive Card and Slack Block Kit formats can be added by
/// overriding BuildPayload().
/// </summary>
public class WebhookAlertProvider : IAlertProvider
{
    private readonly WebhookAlertConfig _config;
    private readonly HttpClient _http;
    private readonly ILogger<WebhookAlertProvider> _logger;

    public WebhookAlertProvider(WebhookAlertConfig config, ILogger<WebhookAlertProvider> logger)
    {
        _config = config;
        _logger = logger;
        _http = new HttpClient();
        foreach (var (key, value) in config.Headers)
            _http.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
    }

    public string ProviderName => "Webhook";

    public async Task SendAlertAsync(AlertModel alert, CancellationToken ct = default)
    {
        if (!_config.Enabled) return;
        if (alert.Severity < _config.MinimumSeverity) return;

        var payload = BuildPayload(alert);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            var response = await _http.PostAsync(_config.Url, content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Webhook returned {Status} for {System}: {Body}",
                    (int)response.StatusCode, alert.SystemName, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook POST failed for {System}", alert.SystemName);
            throw;
        }
    }

    private static string BuildPayload(AlertModel alert)
    {
        // Generic JSON payload — adapt for Teams/Slack as needed
        var obj = new
        {
            timestamp   = alert.TriggeredAt,
            severity    = alert.Severity.ToString(),
            system      = alert.SystemName,
            category    = alert.Category,
            previous    = alert.PreviousStatus.ToString(),
            current     = alert.CurrentStatus.ToString(),
            message     = alert.Message,
            errorDetails = alert.ErrorDetails
        };
        return JsonSerializer.Serialize(obj);
    }
}
