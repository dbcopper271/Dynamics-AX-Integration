using Microsoft.AspNetCore.Mvc;
using MonitoringTool.Models;
using MonitoringTool.Services;

namespace MonitoringTool.Api;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly MonitoringOrchestrator _orchestrator;

    public HealthController(MonitoringOrchestrator orchestrator)
        => _orchestrator = orchestrator;

    /// <summary>Returns latest health check results for all monitored systems.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var results = await _orchestrator.GetLatestResultsAsync();
        return Ok(results.Select(r => new
        {
            r.SystemName,
            r.Category,
            Status        = r.Status.ToString(),
            r.Message,
            CheckedAt     = r.CheckedAt.ToString("o"),
            ResponseMs    = (int)r.ResponseTime.TotalMilliseconds,
            r.Metrics,
            r.ErrorDetails
        }));
    }

    /// <summary>Returns summary counts by status.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var results = await _orchestrator.GetLatestResultsAsync();
        return Ok(new
        {
            Healthy   = results.Count(r => r.Status == HealthStatus.Healthy),
            Degraded  = results.Count(r => r.Status == HealthStatus.Degraded),
            Unhealthy = results.Count(r => r.Status == HealthStatus.Unhealthy),
            Unknown   = results.Count(r => r.Status == HealthStatus.Unknown),
            Total     = results.Count,
            AsOf      = DateTime.UtcNow.ToString("o")
        });
    }

    /// <summary>Returns results for a single system.</summary>
    [HttpGet("{systemName}")]
    public async Task<IActionResult> GetSystem(string systemName)
    {
        var results = await _orchestrator.GetLatestResultsAsync();
        var result  = results.FirstOrDefault(r =>
            r.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase));

        if (result is null) return NotFound(new { error = $"System '{systemName}' not found." });
        return Ok(result);
    }
}
