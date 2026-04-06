using System.Net;
using System.Net.Mail;
using System.Text;
using MonitoringTool.Interfaces;
using MonitoringTool.Models;
using Microsoft.Extensions.Logging;

namespace MonitoringTool.Alerts;

/// <summary>
/// Sends alert emails via SMTP.
/// Configure SmtpHost, port, credentials, and recipient list in appsettings.json.
/// </summary>
public class EmailAlertProvider : IAlertProvider
{
    private readonly EmailAlertConfig _config;
    private readonly ILogger<EmailAlertProvider> _logger;

    public EmailAlertProvider(EmailAlertConfig config, ILogger<EmailAlertProvider> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string ProviderName => "Email";

    public async Task SendAlertAsync(AlertModel alert, CancellationToken ct = default)
    {
        if (!_config.Enabled) return;
        if (alert.Severity < _config.MinimumSeverity) return;
        if (_config.ToAddresses.Count == 0) return;

        try
        {
            using var smtp = new SmtpClient(_config.SmtpHost, _config.SmtpPort)
            {
                EnableSsl   = _config.UseSsl,
                Credentials = new NetworkCredential(_config.Username, _config.Password)
            };

            using var mail = new MailMessage
            {
                From       = new MailAddress(_config.FromAddress, _config.FromName),
                Subject    = BuildSubject(alert),
                Body       = BuildBody(alert),
                IsBodyHtml = true
            };

            foreach (var to in _config.ToAddresses)
                mail.To.Add(to);

            await smtp.SendMailAsync(mail, ct);
            _logger.LogInformation("Alert email sent for {System} to {Recipients}",
                alert.SystemName, string.Join(", ", _config.ToAddresses));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send alert email for {System}", alert.SystemName);
            throw;
        }
    }

    private static string BuildSubject(AlertModel alert)
    {
        var emoji = alert.Severity switch
        {
            AlertSeverity.Critical => "[CRITICAL]",
            AlertSeverity.Warning  => "[WARNING]",
            _                      => "[RECOVERY]"
        };
        return $"{emoji} IT Monitor: {alert.SystemName} — {alert.CurrentStatus}";
    }

    private static string BuildBody(AlertModel alert)
    {
        var color = alert.Severity switch
        {
            AlertSeverity.Critical => "#d32f2f",
            AlertSeverity.Warning  => "#f57c00",
            _                      => "#388e3c"
        };

        var sb = new StringBuilder();
        sb.AppendLine("<html><body style='font-family:Arial,sans-serif;'>");
        sb.AppendLine($"<h2 style='color:{color};'>IT Monitoring Alert</h2>");
        sb.AppendLine("<table cellpadding='6' cellspacing='0' border='1' style='border-collapse:collapse;'>");
        Row(sb, "System",     alert.SystemName);
        Row(sb, "Category",   alert.Category);
        Row(sb, "Severity",   alert.Severity.ToString());
        Row(sb, "Status",     $"{alert.PreviousStatus} → <strong>{alert.CurrentStatus}</strong>");
        Row(sb, "Message",    alert.Message);
        Row(sb, "Time (UTC)", alert.TriggeredAt.ToString("yyyy-MM-dd HH:mm:ss"));
        if (!string.IsNullOrEmpty(alert.ErrorDetails))
            Row(sb, "Details", $"<pre style='font-size:11px;'>{System.Net.WebUtility.HtmlEncode(alert.ErrorDetails)}</pre>");
        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }

    private static void Row(StringBuilder sb, string label, string value)
        => sb.AppendLine($"<tr><td><strong>{label}</strong></td><td>{value}</td></tr>");
}
