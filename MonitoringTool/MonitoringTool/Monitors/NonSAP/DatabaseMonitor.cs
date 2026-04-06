using Microsoft.Data.SqlClient;
using MonitoringTool.Models;
using MonitoringTool.Monitors;
using Microsoft.Extensions.Logging;

namespace MonitoringTool.Monitors.NonSAP;

/// <summary>
/// Monitors relational database health by executing a lightweight probe query
/// and measuring response time.
///
/// Supports: SQL Server (native), Oracle/MySQL/PostgreSQL (stub — add the
/// relevant NuGet package and implement the provider switch).
/// </summary>
public class DatabaseMonitor : BaseMonitor
{
    private readonly DatabaseConfig _config;

    public DatabaseMonitor(DatabaseConfig config, ILogger<DatabaseMonitor> logger)
        : base(logger)
    {
        _config = config;
    }

    public override string SystemName => _config.Name;
    public override string Category => $"Database ({_config.Type})";
    public override bool IsEnabled => _config.Enabled;

    protected override async Task<MonitoringResult> ExecuteCheckAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await ProbeAsync(ct);
            sw.Stop();
        }
        catch (Exception ex)
        {
            sw.Stop();
            return MonitoringResult.Unhealthy(SystemName, Category,
                $"Database unreachable: {ex.Message}", ex);
        }

        int ms = (int)sw.ElapsedMilliseconds;
        var result = new MonitoringResult
        {
            SystemName = SystemName,
            Category = Category,
            CheckedAt = DateTime.UtcNow
        };
        result.Metrics["QueryResponseMs"] = ms;

        if (ms >= _config.ResponseTimeCriticalMs)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = $"Query response {ms}ms exceeds critical threshold {_config.ResponseTimeCriticalMs}ms";
        }
        else if (ms >= _config.ResponseTimeWarningMs)
        {
            result.Status = HealthStatus.Degraded;
            result.Message = $"Query response {ms}ms exceeds warning threshold {_config.ResponseTimeWarningMs}ms";
        }
        else
        {
            result.Status = HealthStatus.Healthy;
            result.Message = $"Database healthy. Probe query: {ms}ms";
        }

        return result;
    }

    private async Task ProbeAsync(CancellationToken ct)
    {
        switch (_config.Type)
        {
            case DatabaseType.SqlServer:
                await ProbeSqlServerAsync(ct);
                break;

            case DatabaseType.Oracle:
            case DatabaseType.MySql:
            case DatabaseType.PostgreSql:
                // Add the corresponding NuGet package and implement here:
                //   Oracle: Oracle.ManagedDataAccess.Core
                //   MySQL:  MySqlConnector
                //   PgSQL:  Npgsql
                Logger.LogWarning("Database type {Type} probe not yet implemented for {Name}. Skipping.", _config.Type, _config.Name);
                break;

            default:
                throw new NotSupportedException($"Database type {_config.Type} is not supported.");
        }
    }

    private async Task ProbeSqlServerAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(_config.ConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = _config.ProbeQuery;
        cmd.CommandTimeout = 10;
        await cmd.ExecuteScalarAsync(ct);
    }
}
