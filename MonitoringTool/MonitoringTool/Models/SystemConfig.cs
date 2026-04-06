namespace MonitoringTool.Models;

// ────────────────────────────────────────────────────────────────────────────────
// Root configuration model (mirrors appsettings.json structure)
// ────────────────────────────────────────────────────────────────────────────────

public class MonitoringConfig
{
    public int PollingIntervalSeconds { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 30;
    public List<SapSystemConfig> SapSystems { get; set; } = new();
    public List<DynamicsAxConfig> DynamicsAxSystems { get; set; } = new();
    public List<DatabaseConfig> Databases { get; set; } = new();
    public List<WebApiConfig> WebApis { get; set; } = new();
    public List<WindowsServiceConfig> WindowsServices { get; set; } = new();
    public List<FileSystemConfig> FileSystems { get; set; } = new();
    public AlertConfig Alerts { get; set; } = new();
}

// ── SAP ──────────────────────────────────────────────────────────────────────

public class SapSystemConfig
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = string.Empty;
    public int SystemNumber { get; set; } = 0;
    public string Client { get; set; } = "100";
    public string SystemId { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Language { get; set; } = "EN";

    /// <summary>Threshold (ms) to flag login as degraded.</summary>
    public int ResponseTimeWarningMs { get; set; } = 3000;

    /// <summary>Threshold (ms) to flag login as unhealthy.</summary>
    public int ResponseTimeCriticalMs { get; set; } = 8000;

    /// <summary>Monitor transport queue depth.</summary>
    public bool MonitorTransports { get; set; } = true;

    /// <summary>Alert when queue depth exceeds this.</summary>
    public int TransportQueueWarningThreshold { get; set; } = 10;

    /// <summary>Monitor ABAP short dumps in the last N minutes.</summary>
    public bool MonitorShortDumps { get; set; } = true;
    public int ShortDumpWindowMinutes { get; set; } = 60;
    public int ShortDumpWarningThreshold { get; set; } = 5;

    /// <summary>Monitor work process availability.</summary>
    public bool MonitorWorkProcesses { get; set; } = true;
    public int MinFreeWorkProcessPercent { get; set; } = 20;
}

// ── Dynamics AX / D365 ───────────────────────────────────────────────────────

public class DynamicsAxConfig
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public int ResponseTimeWarningMs { get; set; } = 5000;
    public int ResponseTimeCriticalMs { get; set; } = 15000;

    /// <summary>List of OData entity endpoints to probe.</summary>
    public List<string> ProbeEntities { get; set; } = new();
}

// ── Databases ────────────────────────────────────────────────────────────────

public enum DatabaseType { SqlServer, Oracle, MySql, PostgreSql }

public class DatabaseConfig
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DatabaseType Type { get; set; } = DatabaseType.SqlServer;
    public string ConnectionString { get; set; } = string.Empty;
    public int ResponseTimeWarningMs { get; set; } = 1000;
    public int ResponseTimeCriticalMs { get; set; } = 5000;

    /// <summary>Custom SQL probe query (should be lightweight, e.g. SELECT 1).</summary>
    public string ProbeQuery { get; set; } = "SELECT 1";
}

// ── Web APIs ─────────────────────────────────────────────────────────────────

public class WebApiConfig
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public Dictionary<string, string> Headers { get; set; } = new();
    public int ResponseTimeWarningMs { get; set; } = 2000;
    public int ResponseTimeCriticalMs { get; set; } = 8000;
    public List<int> ExpectedStatusCodes { get; set; } = new() { 200 };
    public string? ExpectedBodyContains { get; set; }
}

// ── Windows Services ─────────────────────────────────────────────────────────

public class WindowsServiceConfig
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    /// <summary>Windows service name (as seen in services.msc).</summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>Hostname where the service runs (null = local machine).</summary>
    public string? MachineName { get; set; }
}

// ── File Systems ─────────────────────────────────────────────────────────────

public class FileSystemConfig
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string Path { get; set; } = string.Empty;

    /// <summary>Minimum free space in MB before flagging as degraded.</summary>
    public long FreeSpaceWarningMB { get; set; } = 1024;

    /// <summary>Minimum free space in MB before flagging as unhealthy.</summary>
    public long FreeSpaceCriticalMB { get; set; } = 256;

    /// <summary>If set, alert when no new files appear within this window.</summary>
    public int? StaleFileWindowMinutes { get; set; }
}

// ── Alerts ───────────────────────────────────────────────────────────────────

public class AlertConfig
{
    public EmailAlertConfig Email { get; set; } = new();
    public WebhookAlertConfig Webhook { get; set; } = new();
    public bool EnableConsoleAlerts { get; set; } = true;
    public bool EnableFileLog { get; set; } = true;
    public string LogFilePath { get; set; } = "logs/monitoring.log";
}

public class EmailAlertConfig
{
    public bool Enabled { get; set; } = false;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "IT Monitoring";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public List<string> ToAddresses { get; set; } = new();
    public AlertSeverity MinimumSeverity { get; set; } = AlertSeverity.Warning;
}

public class WebhookAlertConfig
{
    public bool Enabled { get; set; } = false;
    public string Url { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public AlertSeverity MinimumSeverity { get; set; } = AlertSeverity.Warning;
}
