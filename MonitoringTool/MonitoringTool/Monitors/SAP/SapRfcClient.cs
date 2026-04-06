using MonitoringTool.Models;
using Microsoft.Extensions.Logging;

namespace MonitoringTool.Monitors.SAP;

/// <summary>
/// Abstraction layer over SAP RFC calls via SAP .NET Connector (NCo) 3.x.
///
/// ══════════════════════════════════════════════════════════════════
/// HOW TO ACTIVATE REAL SAP CONNECTIVITY
/// ══════════════════════════════════════════════════════════════════
/// 1. Install SAP NCo 3.x for your platform (sapnco.dll + sapnco_utils.dll).
///    Download from SAP Service Marketplace (S-user required):
///    https://support.sap.com/en/product/connectors/msnet.html
///
/// 2. Add references to the DLLs in MonitoringTool.csproj:
///    <Reference Include="sapnco">
///      <HintPath>lib\sapnco.dll</HintPath>
///    </Reference>
///    <Reference Include="sapnco_utils">
///      <HintPath>lib\sapnco_utils.dll</HintPath>
///    </Reference>
///
/// 3. Configure the RFC destination in sapnco.cfg (or programmatically):
///    [DEST:SAP-PRD]
///    TYPE=A
///    ASHOST=sap-prd.company.com
///    SYSNR=00
///    CLIENT=100
///    USER=MONITOR_USER
///    PASSWD=SECRET
///    LANG=EN
///
/// 4. Uncomment the #define below to switch from stubs to real NCo calls.
///    #define USE_SAP_NCO
/// ══════════════════════════════════════════════════════════════════
///
/// ABAP backend FMs called (all in Function Group ZMON):
///   Z_MON_GET_SYSTEM_HEALTH    → ping + system info
///   Z_MON_GET_WORK_PROCESSES   → WP availability %
///   Z_MON_GET_SHORT_DUMPS      → ABAP runtime errors
///   Z_MON_GET_TRANSPORT_QUEUE  → STMS pending transports
///   Z_MON_GET_BATCH_JOBS       → cancelled/active batch jobs
///   Z_MON_GET_IDOC_STATUS      → IDoc error rate
///   Z_MON_GET_SYSLOG           → system log Abort/Error count
/// </summary>
public class SapRfcClient
{
    private readonly SapSystemConfig _config;
    private readonly ILogger _logger;

    // For simulation only – remove when USE_SAP_NCO is active
    private static readonly Random _rng = new();

    public SapRfcClient(SapSystemConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Ping / Login check  →  Z_MON_GET_SYSTEM_HEALTH
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calls Z_MON_GET_SYSTEM_HEALTH via RFC.
    /// Returns (success, responseTimeMs, errorMessage).
    /// </summary>
    public async Task<(bool Connected, int ResponseMs, string? Error)> PingAsync(CancellationToken ct)
    {
        _logger.LogDebug("SAP Ping → {Host} SysNr={SysNr}", _config.Host, _config.SystemNumber);

#if USE_SAP_NCO
        // ── REAL NCo IMPLEMENTATION ──────────────────────────────────────────
        return await Task.Run(() =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var dest = SAP.Middleware.Connector.RfcDestinationManager
                               .GetDestination(_config.Name);

                var fn = dest.Repository.CreateFunction("Z_MON_GET_SYSTEM_HEALTH");
                fn.Invoke(dest);
                sw.Stop();

                string status  = fn.GetValue<string>("EV_STATUS");
                string message = fn.GetValue<string>("EV_MESSAGE");

                bool ok = status == "HEALTHY" || status == "DEGRADED";
                return (ok, (int)sw.ElapsedMilliseconds, ok ? null : message);
            }
            catch (SAP.Middleware.Connector.RfcCommunicationException ex)
            {
                sw.Stop();
                return (false, 0, $"RFC communication error: {ex.Message}");
            }
            catch (SAP.Middleware.Connector.RfcLogonException ex)
            {
                sw.Stop();
                return (false, 0, $"RFC logon failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                sw.Stop();
                return (false, 0, ex.Message);
            }
        }, ct);
        // ────────────────────────────────────────────────────────────────────
#else
        // ── STUB (simulated) ─────────────────────────────────────────────────
        await Task.Delay(_rng.Next(80, 300), ct);
        if (_rng.Next(100) < 3) return (false, 0, "RFC connection refused (simulated)");
        return (true, _rng.Next(80, 500), null);
        // ────────────────────────────────────────────────────────────────────
#endif
    }

    // ══════════════════════════════════════════════════════════════════════
    // Work processes  →  Z_MON_GET_WORK_PROCESSES
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Returns % of free Dialog work processes.</summary>
    public async Task<(double FreePercent, string? Error)> GetFreeWorkProcessPercentAsync(CancellationToken ct)
    {
        _logger.LogDebug("SAP WP check → {Name}", _config.Name);

#if USE_SAP_NCO
        return await Task.Run(() =>
        {
            try
            {
                var dest = SAP.Middleware.Connector.RfcDestinationManager
                               .GetDestination(_config.Name);
                var fn = dest.Repository.CreateFunction("Z_MON_GET_WORK_PROCESSES");
                fn.Invoke(dest);

                double pct = Convert.ToDouble(fn.GetValue<string>("EV_FREE_DIA_PCT"));
                return (pct, (string?)null);
            }
            catch (Exception ex)
            {
                return (0.0, ex.Message);
            }
        }, ct);
#else
        await Task.Delay(50, ct);
        return (_rng.NextDouble() * 100, null);
#endif
    }

    // ══════════════════════════════════════════════════════════════════════
    // Short dumps  →  Z_MON_GET_SHORT_DUMPS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Returns number of ABAP short dumps in the given time window.</summary>
    public async Task<(int Count, string? Error)> GetShortDumpCountAsync(int windowMinutes, CancellationToken ct)
    {
        _logger.LogDebug("SAP short dump check → {Name} window={Min}min", _config.Name, windowMinutes);

#if USE_SAP_NCO
        return await Task.Run(() =>
        {
            try
            {
                var dest = SAP.Middleware.Connector.RfcDestinationManager
                               .GetDestination(_config.Name);
                var fn = dest.Repository.CreateFunction("Z_MON_GET_SHORT_DUMPS");
                fn.SetValue("IV_WINDOW_MINUTES", windowMinutes);
                fn.SetValue("IV_MAX_ROWS", 500);
                fn.Invoke(dest);

                int count = fn.GetValue<int>("EV_DUMP_COUNT");
                return (count, (string?)null);
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }, ct);
#else
        await Task.Delay(50, ct);
        return (_rng.Next(0, 8), null);
#endif
    }

    // ══════════════════════════════════════════════════════════════════════
    // Transport queue  →  Z_MON_GET_TRANSPORT_QUEUE
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Returns number of pending transport requests in the import queue.</summary>
    public async Task<(int QueueDepth, string? Error)> GetTransportQueueDepthAsync(CancellationToken ct)
    {
        _logger.LogDebug("SAP transport check → {Name}", _config.Name);

#if USE_SAP_NCO
        return await Task.Run(() =>
        {
            try
            {
                var dest = SAP.Middleware.Connector.RfcDestinationManager
                               .GetDestination(_config.Name);
                var fn = dest.Repository.CreateFunction("Z_MON_GET_TRANSPORT_QUEUE");
                // Optionally set target system filter
                // fn.SetValue("IV_TARGET_SYS", _config.SystemId);
                fn.Invoke(dest);

                int depth = fn.GetValue<int>("EV_QUEUE_DEPTH");
                return (depth, (string?)null);
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }, ct);
#else
        await Task.Delay(50, ct);
        return (_rng.Next(0, 15), null);
#endif
    }

    // ══════════════════════════════════════════════════════════════════════
    // Batch jobs  →  Z_MON_GET_BATCH_JOBS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Returns count of cancelled batch jobs in the given window.</summary>
    public async Task<(int CancelledCount, int ActiveCount, string? Error)> GetBatchJobStatusAsync(
        int windowMinutes, CancellationToken ct)
    {
        _logger.LogDebug("SAP batch job check → {Name}", _config.Name);

#if USE_SAP_NCO
        return await Task.Run(() =>
        {
            try
            {
                var dest = SAP.Middleware.Connector.RfcDestinationManager
                               .GetDestination(_config.Name);
                var fn = dest.Repository.CreateFunction("Z_MON_GET_BATCH_JOBS");
                fn.SetValue("IV_WINDOW_MINUTES", windowMinutes);
                fn.Invoke(dest);

                int cancelled = fn.GetValue<int>("EV_ABORTED_COUNT");
                int active    = fn.GetValue<int>("EV_ACTIVE_COUNT");
                return (cancelled, active, (string?)null);
            }
            catch (Exception ex)
            {
                return (0, 0, ex.Message);
            }
        }, ct);
#else
        await Task.Delay(50, ct);
        return (_rng.Next(0, 3), _rng.Next(0, 10), null);
#endif
    }

    // ══════════════════════════════════════════════════════════════════════
    // IDoc status  →  Z_MON_GET_IDOC_STATUS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Returns IDoc error rate % in the given time window.</summary>
    public async Task<(double ErrorRatePct, int ErrorCount, int TotalCount, string? Error)>
        GetIdocErrorRateAsync(int windowMinutes, CancellationToken ct)
    {
        _logger.LogDebug("SAP IDoc check → {Name}", _config.Name);

#if USE_SAP_NCO
        return await Task.Run(() =>
        {
            try
            {
                var dest = SAP.Middleware.Connector.RfcDestinationManager
                               .GetDestination(_config.Name);
                var fn = dest.Repository.CreateFunction("Z_MON_GET_IDOC_STATUS");
                fn.SetValue("IV_WINDOW_MINUTES", windowMinutes);
                fn.Invoke(dest);

                int total    = fn.GetValue<int>("EV_TOTAL_IDOCS");
                int errors   = fn.GetValue<int>("EV_ERROR_COUNT");
                double rate  = fn.GetValue<double>("EV_ERROR_RATE_PCT");
                return (rate, errors, total, (string?)null);
            }
            catch (Exception ex)
            {
                return (0.0, 0, 0, ex.Message);
            }
        }, ct);
#else
        await Task.Delay(60, ct);
        int total  = _rng.Next(0, 500);
        int errors = _rng.Next(0, total > 0 ? total / 5 : 1);
        double rate = total > 0 ? (errors * 100.0 / total) : 0;
        return (rate, errors, total, null);
#endif
    }

    // ══════════════════════════════════════════════════════════════════════
    // System log  →  Z_MON_GET_SYSLOG
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Returns abort/error/warning counts from the SAP syslog.</summary>
    public async Task<(int AbortCount, int ErrorCount, int WarnCount, string? Error)>
        GetSyslogCountsAsync(int windowMinutes, CancellationToken ct)
    {
        _logger.LogDebug("SAP syslog check → {Name}", _config.Name);

#if USE_SAP_NCO
        return await Task.Run(() =>
        {
            try
            {
                var dest = SAP.Middleware.Connector.RfcDestinationManager
                               .GetDestination(_config.Name);
                var fn = dest.Repository.CreateFunction("Z_MON_GET_SYSLOG");
                fn.SetValue("IV_WINDOW_MINUTES", windowMinutes);
                fn.SetValue("IV_MIN_CLASS", "W");
                fn.Invoke(dest);

                int aborts = fn.GetValue<int>("EV_ABORT_COUNT");
                int errors = fn.GetValue<int>("EV_ERROR_COUNT");
                int warns  = fn.GetValue<int>("EV_WARN_COUNT");
                return (aborts, errors, warns, (string?)null);
            }
            catch (Exception ex)
            {
                return (0, 0, 0, ex.Message);
            }
        }, ct);
#else
        await Task.Delay(50, ct);
        return (_rng.Next(0, 2), _rng.Next(0, 5), _rng.Next(0, 20), null);
#endif
    }

    // ══════════════════════════════════════════════════════════════════════
    // System info  →  Z_MON_GET_SYSTEM_HEALTH
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Returns SAP system information (release, kernel, host).</summary>
    public async Task<Dictionary<string, string>> GetSystemInfoAsync(CancellationToken ct)
    {
        _logger.LogDebug("SAP system info → {Name}", _config.Name);

#if USE_SAP_NCO
        return await Task.Run(() =>
        {
            try
            {
                var dest = SAP.Middleware.Connector.RfcDestinationManager
                               .GetDestination(_config.Name);
                var fn = dest.Repository.CreateFunction("Z_MON_GET_SYSTEM_HEALTH");
                fn.Invoke(dest);

                var health = fn.GetStructure("ES_HEALTH");
                return new Dictionary<string, string>
                {
                    ["SapRelease"]    = health.GetValue<string>("SAP_RELEASE"),
                    ["KernelRelease"] = health.GetValue<string>("KERNEL_REL"),
                    ["Host"]          = health.GetValue<string>("HOST"),
                    ["SID"]           = health.GetValue<string>("SID"),
                    ["Client"]        = health.GetValue<string>("CLIENT"),
                    ["DBType"]        = health.GetValue<string>("DB_TYPE"),
                    ["DBHost"]        = health.GetValue<string>("DB_HOST"),
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, string>
                {
                    ["Error"] = ex.Message
                };
            }
        }, ct);
#else
        await Task.Delay(80, ct);
        return new Dictionary<string, string>
        {
            ["SapRelease"]    = "7.56",
            ["KernelRelease"] = "7.56",
            ["Host"]          = _config.Host,
            ["SID"]           = _config.SystemId,
            ["Client"]        = _config.Client,
            ["DBType"]        = "HDB",
            ["DBHost"]        = _config.Host
        };
#endif
    }
}
