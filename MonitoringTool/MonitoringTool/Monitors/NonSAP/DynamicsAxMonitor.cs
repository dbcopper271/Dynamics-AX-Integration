using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MonitoringTool.Models;
using MonitoringTool.Monitors;
using Microsoft.Extensions.Logging;

namespace MonitoringTool.Monitors.NonSAP;

/// <summary>
/// Monitors Dynamics AX / D365 Finance &amp; Operations availability via:
///  - OAuth2 token acquisition
///  - OData entity endpoint probes
///  - Response time thresholds
/// </summary>
public class DynamicsAxMonitor : BaseMonitor
{
    private readonly DynamicsAxConfig _config;
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public DynamicsAxMonitor(DynamicsAxConfig config, ILogger<DynamicsAxMonitor> logger)
        : base(logger)
    {
        _config = config;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public override string SystemName => _config.Name;
    public override string Category => "Dynamics AX / D365";
    public override bool IsEnabled => _config.Enabled;

    protected override async Task<MonitoringResult> ExecuteCheckAsync(CancellationToken ct)
    {
        var result = new MonitoringResult
        {
            SystemName = SystemName,
            Category = Category,
            CheckedAt = DateTime.UtcNow
        };

        // 1. Acquire OAuth token
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string? token;
        try
        {
            token = await AcquireTokenAsync(ct);
        }
        catch (Exception ex)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = $"OAuth token acquisition failed: {ex.Message}";
            result.ErrorDetails = ex.ToString();
            return result;
        }
        sw.Stop();
        result.Metrics["TokenAcquisitionMs"] = (int)sw.ElapsedMilliseconds;

        if (string.IsNullOrEmpty(token))
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = "OAuth token acquisition returned empty token.";
            return result;
        }

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 2. Probe OData entity endpoints
        var failedProbes = new List<string>();
        var degradedProbes = new List<string>();
        int totalProbeMs = 0;

        var probeEntities = _config.ProbeEntities.Count > 0
            ? _config.ProbeEntities
            : new List<string> { "SystemParameters" }; // minimal default probe

        foreach (var entity in probeEntities)
        {
            var url = $"{_config.BaseUrl.TrimEnd('/')}/data/{entity}?$top=1";
            var probeStart = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var response = await _http.GetAsync(url, ct);
                probeStart.Stop();
                int probeMs = (int)probeStart.ElapsedMilliseconds;
                totalProbeMs += probeMs;
                result.Metrics[$"Probe_{entity}_Ms"] = probeMs;

                if (!response.IsSuccessStatusCode)
                {
                    failedProbes.Add($"{entity} → HTTP {(int)response.StatusCode}");
                }
                else if (probeMs >= _config.ResponseTimeCriticalMs)
                {
                    failedProbes.Add($"{entity} response {probeMs}ms (critical threshold {_config.ResponseTimeCriticalMs}ms)");
                }
                else if (probeMs >= _config.ResponseTimeWarningMs)
                {
                    degradedProbes.Add($"{entity} slow: {probeMs}ms");
                }
            }
            catch (Exception ex)
            {
                probeStart.Stop();
                failedProbes.Add($"{entity} → {ex.Message}");
            }
        }

        // 3. Determine overall status
        if (failedProbes.Count > 0)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = "Failed probes: " + string.Join("; ", failedProbes);
        }
        else if (degradedProbes.Count > 0)
        {
            result.Status = HealthStatus.Degraded;
            result.Message = "Slow probes: " + string.Join("; ", degradedProbes);
        }
        else
        {
            result.Status = HealthStatus.Healthy;
            result.Message = $"D365 healthy. {probeEntities.Count} endpoint(s) OK. Avg: {totalProbeMs / probeEntities.Count}ms";
        }

        return result;
    }

    private async Task<string?> AcquireTokenAsync(CancellationToken ct)
    {
        // Support both real OAuth and bypass for environments that don't require auth
        if (string.IsNullOrWhiteSpace(_config.TenantId))
        {
            Logger.LogDebug("D365 OAuth skipped — no TenantId configured for {Name}", _config.Name);
            return "bypass";
        }

        var tokenUrl = $"https://login.microsoftonline.com/{_config.TenantId}/oauth2/token";
        var body = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string,string>("grant_type",    "client_credentials"),
            new KeyValuePair<string,string>("client_id",     _config.ClientId),
            new KeyValuePair<string,string>("client_secret", _config.ClientSecret),
            new KeyValuePair<string,string>("resource",      _config.Resource)
        });

        var response = await _http.PostAsync(tokenUrl, body, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString();
    }
}
