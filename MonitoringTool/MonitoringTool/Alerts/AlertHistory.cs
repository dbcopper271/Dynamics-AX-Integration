using MonitoringTool.Models;

namespace MonitoringTool.Alerts;

/// <summary>
/// In-memory circular buffer of recent alerts.
/// Thread-safe. Keeps last N alerts.
/// </summary>
public class AlertHistory
{
    private readonly int _capacity;
    private readonly LinkedList<AlertModel> _alerts = new();
    private readonly object _lock = new();

    public AlertHistory(int capacity = 200)
        => _capacity = capacity;

    public void Add(AlertModel alert)
    {
        lock (_lock)
        {
            _alerts.AddFirst(alert);
            if (_alerts.Count > _capacity)
                _alerts.RemoveLast();
        }
    }

    public IReadOnlyList<AlertModel> GetRecent(int limit = 50)
    {
        lock (_lock)
        {
            return _alerts.Take(limit).ToList();
        }
    }
}
