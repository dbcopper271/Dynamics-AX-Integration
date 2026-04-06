using MonitoringTool.Models;
using MonitoringTool.Services;

namespace MonitoringTool.Dashboard;

/// <summary>
/// Renders a live-updating console dashboard using ANSI escape codes.
/// Refreshes whenever the orchestrator fires a ResultUpdated event.
/// </summary>
public class ConsoleDashboard
{
    private readonly MonitoringOrchestrator _orchestrator;
    private readonly object _renderLock = new();
    private readonly DateTime _startTime = DateTime.UtcNow;

    public ConsoleDashboard(MonitoringOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        _orchestrator.ResultUpdated += _ => Render();
    }

    public void Start()
    {
        Console.CursorVisible = false;
        Console.Clear();
        PrintHeader();
    }

    public void Stop()
    {
        Console.CursorVisible = true;
    }

    /// <summary>Full re-render of the dashboard.</summary>
    public void Render()
    {
        lock (_renderLock)
        {
            Console.Clear();
            PrintHeader();
            var results = _orchestrator.GetLatestResultsAsync().GetAwaiter().GetResult();
            PrintSummary(results);
            PrintTable(results);
            PrintFooter();
        }
    }

    // ── Sections ─────────────────────────────────────────────────────────────

    private void PrintHeader()
    {
        SetColor(ConsoleColor.Cyan);
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              IT SYSTEM MONITORING DASHBOARD  (SAP & Non-SAP)                ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        ResetColor();
        Console.WriteLine($"  Updated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC   Uptime: {FormatUptime()}");
        Console.WriteLine();
    }

    private void PrintSummary(IReadOnlyList<MonitoringResult> results)
    {
        int healthy   = results.Count(r => r.Status == HealthStatus.Healthy);
        int degraded  = results.Count(r => r.Status == HealthStatus.Degraded);
        int unhealthy = results.Count(r => r.Status == HealthStatus.Unhealthy);
        int unknown   = results.Count(r => r.Status == HealthStatus.Unknown);

        Console.Write("  Summary: ");
        WriteColored($"● {healthy} Healthy  ", ConsoleColor.Green);
        WriteColored($"● {degraded} Degraded  ", ConsoleColor.Yellow);
        WriteColored($"● {unhealthy} Unhealthy  ", ConsoleColor.Red);
        WriteColored($"● {unknown} Unknown", ConsoleColor.DarkGray);
        Console.WriteLine();
        Console.WriteLine();
    }

    private void PrintTable(IReadOnlyList<MonitoringResult> results)
    {
        // Column widths
        const int wStatus = 10;
        const int wName   = 25;
        const int wCat    = 20;
        const int wResp   = 9;
        const int wMsg    = 40;

        SetColor(ConsoleColor.DarkGray);
        Console.WriteLine($"  {"STATUS",-wStatus} {"SYSTEM",-wName} {"CATEGORY",-wCat} {"RESP(ms)",wResp} {"MESSAGE",-wMsg}");
        Console.WriteLine($"  {new string('─', wStatus)} {new string('─', wName)} {new string('─', wCat)} {new string('─', wResp)} {new string('─', wMsg)}");
        ResetColor();

        var grouped = results
            .OrderBy(r => r.Category)
            .ThenBy(r => r.SystemName);

        foreach (var r in grouped)
        {
            var (color, label) = r.Status switch
            {
                HealthStatus.Healthy   => (ConsoleColor.Green,   "● HEALTHY  "),
                HealthStatus.Degraded  => (ConsoleColor.Yellow,  "▲ DEGRADED "),
                HealthStatus.Unhealthy => (ConsoleColor.Red,     "✖ UNHEALTHY"),
                _                      => (ConsoleColor.DarkGray,"? UNKNOWN  ")
            };

            int respMs = r.ResponseTime.TotalMilliseconds > 0
                ? (int)r.ResponseTime.TotalMilliseconds : -1;

            string respStr = respMs >= 0 ? $"{respMs,8}" : "       -";
            string msg     = Truncate(r.Message, wMsg);

            WriteColored($"  {label,-wStatus} ", color);
            Console.Write($"{Truncate(r.SystemName, wName),-wName} {Truncate(r.Category, wCat),-wCat} {respStr} {msg,-wMsg}");
            Console.WriteLine();
        }

        Console.WriteLine();
    }

    private void PrintFooter()
    {
        SetColor(ConsoleColor.DarkGray);
        Console.WriteLine("  Press Ctrl+C to stop monitoring.");
        ResetColor();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string FormatUptime()
    {
        var up = DateTime.UtcNow - _startTime;
        return up.TotalHours >= 1
            ? $"{(int)up.TotalHours}h {up.Minutes:D2}m"
            : $"{up.Minutes}m {up.Seconds:D2}s";
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..(max - 1)] + "…";

    private static void WriteColored(string text, ConsoleColor color)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = prev;
    }

    private static void SetColor(ConsoleColor color) => Console.ForegroundColor = color;
    private static void ResetColor() => Console.ResetColor();
}
