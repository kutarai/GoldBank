namespace SynergySwitch.Core.Iso8583;

/// <summary>
/// Configuration for the ISO 8583 TCP connection to the acquiring bank (via Zimswitch).
/// </summary>
public class BankConnectionSettings
{
    public const string SectionName = "BankConnection";

    /// <summary>Bank/Zimswitch host address.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Bank/Zimswitch TCP port.</summary>
    public int Port { get; set; } = 9100;

    /// <summary>TCP connect + read timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Acquiring institution identification code (field 32).</summary>
    public string AcquiringInstitutionId { get; set; } = "000000";

    /// <summary>Network international identifier (field 24) — "002" for Zimswitch.</summary>
    public string NetworkId { get; set; } = "002";

    /// <summary>Country code for acquiring institution (field 19).</summary>
    public string CountryCode { get; set; } = "716";

    /// <summary>If true, send 2-byte message length header (big-endian) before each message.</summary>
    public bool SendLengthHeader { get; set; } = true;

    /// <summary>
    /// Number of persistent TCP connections in the pool.
    /// Each connection handles multiple concurrent in-flight transactions correlated by STAN.
    /// Recommended: 8–16 for up to 10,000 concurrent terminals.
    /// </summary>
    public int PoolSize { get; set; } = 8;

    /// <summary>
    /// Legacy global offline mode — now controlled per-gateway via GatewayEntity.OfflineMode.
    /// Kept for backwards compatibility; ignored when gateways are configured.
    /// </summary>
    [Obsolete("Use per-gateway OfflineMode instead")]
    public bool OfflineMode { get; set; } = true;
}
