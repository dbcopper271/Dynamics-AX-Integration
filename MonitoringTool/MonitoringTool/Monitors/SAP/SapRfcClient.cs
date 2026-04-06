using MonitoringTool.Models;
using Microsoft.Extensions.Logging;

namespace MonitoringTool.Monitors.SAP;

/// <summary>
/// Abstraction layer over SAP RFC calls.
///
/// Currently uses simulated/stubbed responses so the tool compiles and runs
/// without the SAP NCo 3.x native DLLs installed.
///
/// TO ENABLE REAL SAP CONNECTIVITY:
///   1. Install SAP .NET Connector 3.x (NCo) for your platform.
///   2. Add reference to sapnco.dll and sapnco_utils.dll.
///   3. Replace the stub methods below with real NCo calls.
///   4. Add an RFC destination in the app config or sapnco config file.
///
/// Example NCo call:
///   var dest = RfcDestinationManager.GetDestination(destinationName);
///   var fn   = dest.Repository.CreateFunction("RFC_READ_TABLE");
///   fn.GetStructure("OPTIONS").SetValue("TEXT", "WHERE MANDT = '100'");
///   fn.Invoke(dest);
///   var rows = fn.GetTable("DATA");
/// </summary>
public class SapRfcClient
{
    private readonly SapSystemConfig _config;
    private readonly ILogger _logger;

    // Simulated state for demo purposes
    private static readonly Random _rng = new();

    public SapRfcClient(SapSystemConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Attempt a lightweight RFC ping (RFC_PING or logon test).
    /// Returns (success, responseTimeMs, errorMessage).
    /// </summary>
    public async Task<(bool Connected, int ResponseMs, string? Error)> PingAsync(CancellationToken ct)
    {
        _logger.LogDebug("SAP Ping → {Host} SysNr={SysNr}", _config.Host, _config.SystemNumber);

        // ── STUB (replace with real NCo code) ────────────────────────────────
        await Task.Delay(_rng.Next(100, 400), ct);   // simulate network latency

        // Simulate occasional failures for testing
        if (_rng.Next(100) < 3)   // 3% failure rate
            return (false, 0, "RFC connection refused (simulated)");

        int ms = _rng.Next(80, 500);
        return (true, ms, null);
        // ─────────────────────────────────────────────────────────────────────
    }

    /// <summary>
    /// Returns percentage of free work processes via TH_WPINFO BAPI.
    /// </summary>
    public async Task<(double FreePercent, string? Error)> GetFreeWorkProcessPercentAsync(CancellationToken ct)
    {
        _logger.LogDebug("SAP WP check → {Name}", _config.Name);

        // ── STUB ─────────────────────────────────────────────────────────────
        await Task.Delay(50, ct);
        double free = _rng.NextDouble() * 100;
        return (free, null);
        // ─────────────────────────────────────────────────────────────────────
    }

    /// <summary>
    /// Returns number of ABAP short dumps in the given time window via
    /// reading table SNAPSHOTS or using SAL_ANALYSE_SNAPSHOT.
    /// </summary>
    public async Task<(int Count, string? Error)> GetShortDumpCountAsync(int windowMinutes, CancellationToken ct)
    {
        _logger.LogDebug("SAP short dump check → {Name} window={Min}min", _config.Name, windowMinutes);

        // ── STUB ─────────────────────────────────────────────────────────────
        await Task.Delay(50, ct);
        int count = _rng.Next(0, 8);
        return (count, null);
        // ─────────────────────────────────────────────────────────────────────
    }

    /// <summary>
    /// Returns pending transport queue depth by reading STMS tables.
    /// </summary>
    public async Task<(int QueueDepth, string? Error)> GetTransportQueueDepthAsync(CancellationToken ct)
    {
        _logger.LogDebug("SAP transport check → {Name}", _config.Name);

        // ── STUB ─────────────────────────────────────────────────────────────
        await Task.Delay(50, ct);
        int depth = _rng.Next(0, 15);
        return (depth, null);
        // ─────────────────────────────────────────────────────────────────────
    }

    /// <summary>
    /// Returns SAP system information (release, kernel version, host) via
    /// RFC_SYSTEM_INFO function module.
    /// </summary>
    public async Task<Dictionary<string, string>> GetSystemInfoAsync(CancellationToken ct)
    {
        _logger.LogDebug("SAP system info → {Name}", _config.Name);

        // ── STUB ─────────────────────────────────────────────────────────────
        await Task.Delay(80, ct);
        return new Dictionary<string, string>
        {
            ["SapRelease"] = "7.54",
            ["KernelRelease"] = "7.54",
            ["Host"] = _config.Host,
            ["SID"] = _config.SystemId,
            ["Client"] = _config.Client
        };
        // ─────────────────────────────────────────────────────────────────────
    }
}
