using MonitoringTool.Models;
using MonitoringTool.Monitors;
using Microsoft.Extensions.Logging;

namespace MonitoringTool.Monitors.NonSAP;

/// <summary>
/// Monitors HTTP/HTTPS endpoints (REST APIs, health endpoints, web applications).
/// Checks:
///  - HTTP status code against expected values
///  - Response time thresholds
///  - Optional body content assertion
/// </summary>
public class WebApiMonitor : BaseMonitor
{
    private readonly WebApiConfig _config;
    private readonly HttpClient _http;

    public WebApiMonitor(WebApiConfig config, ILogger<WebApiMonitor> logger)
        : base(logger)
    {
        _config = config;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        foreach (var (key, value) in config.Headers)
            _http.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
    }

    public override string SystemName => _config.Name;
    public override string Category => "Web API / Service";
    public override bool IsEnabled => _config.Enabled;

    protected override async Task<MonitoringResult> ExecuteCheckAsync(CancellationToken ct)
    {
        var result = new MonitoringResult
        {
            SystemName = SystemName,
            Category = Category,
            CheckedAt = DateTime.UtcNow
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        HttpResponseMessage response;
        string body = string.Empty;

        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(_config.Method), _config.Url);
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            body = await response.Content.ReadAsStringAsync(ct);
            sw.Stop();
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.Status = HealthStatus.Unhealthy;
            result.Message = $"Request failed: {ex.Message}";
            result.ErrorDetails = ex.ToString();
            return result;
        }

        int ms = (int)sw.ElapsedMilliseconds;
        int statusCode = (int)response.StatusCode;
        result.Metrics["HttpStatusCode"] = statusCode;
        result.Metrics["ResponseMs"] = ms;

        // Check status code
        var expectedCodes = _config.ExpectedStatusCodes.Count > 0
            ? _config.ExpectedStatusCodes
            : new List<int> { 200 };

        bool statusOk = expectedCodes.Contains(statusCode);

        // Check body assertion
        bool bodyOk = string.IsNullOrEmpty(_config.ExpectedBodyContains)
            || body.Contains(_config.ExpectedBodyContains, StringComparison.OrdinalIgnoreCase);

        if (!statusOk)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = $"Unexpected HTTP {statusCode} (expected: {string.Join(",", expectedCodes)})";
            return result;
        }

        if (!bodyOk)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = $"Response body does not contain expected string: '{_config.ExpectedBodyContains}'";
            return result;
        }

        if (ms >= _config.ResponseTimeCriticalMs)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = $"Response {ms}ms exceeds critical threshold {_config.ResponseTimeCriticalMs}ms";
        }
        else if (ms >= _config.ResponseTimeWarningMs)
        {
            result.Status = HealthStatus.Degraded;
            result.Message = $"Response {ms}ms exceeds warning threshold {_config.ResponseTimeWarningMs}ms";
        }
        else
        {
            result.Status = HealthStatus.Healthy;
            result.Message = $"HTTP {statusCode} OK in {ms}ms";
        }

        return result;
    }
}
