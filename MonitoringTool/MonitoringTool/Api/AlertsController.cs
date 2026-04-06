using Microsoft.AspNetCore.Mvc;
using MonitoringTool.Alerts;

namespace MonitoringTool.Api;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly AlertHistory _history;

    public AlertsController(AlertHistory history) => _history = history;

    /// <summary>Returns recent alerts (newest first).</summary>
    [HttpGet]
    public IActionResult GetAlerts([FromQuery] int limit = 50)
    {
        var alerts = _history.GetRecent(limit);
        return Ok(alerts.Select(a => new
        {
            a.Id,
            a.SystemName,
            a.Category,
            Severity       = a.Severity.ToString(),
            CurrentStatus  = a.CurrentStatus.ToString(),
            PreviousStatus = a.PreviousStatus.ToString(),
            a.Message,
            TriggeredAt    = a.TriggeredAt.ToString("o"),
            a.ErrorDetails
        }));
    }
}
