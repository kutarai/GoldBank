namespace SynergySwitch.Data.Entities;

/// <summary>
/// An ISO 8583 bank gateway endpoint. Multiple gateways provide redundancy
/// and BIN-based routing for card transactions.
/// </summary>
public class GatewayEntity
{
    public int Id { get; set; }

    /// <summary>Unique short name for this gateway (e.g. "ZB-PRIMARY", "CABS-DR").</summary>
    public required string Name { get; set; }

    /// <summary>Protocol used to communicate with this gateway.</summary>
    public GatewayProtocol Protocol { get; set; } = GatewayProtocol.Iso8583;

    /// <summary>Bank/acquirer host address.</summary>
    public required string Host { get; set; }

    /// <summary>TCP port.</summary>
    public int Port { get; set; }

    /// <summary>Acquiring institution ID (ISO 8583 field 32).</summary>
    public required string AcquiringInstitutionId { get; set; }

    /// <summary>Network international identifier (field 24, e.g. "002" Zimswitch).</summary>
    public string NetworkId { get; set; } = "002";

    /// <summary>TCP connection pool size for this gateway.</summary>
    public int PoolSize { get; set; } = 4;

    /// <summary>Connect + response timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Prepend 2-byte length header before each message.</summary>
    public bool SendLengthHeader { get; set; } = true;

    /// <summary>Whether this gateway is enabled for traffic.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>When true, transactions routed to this gateway are approved locally without connecting to the bank.</summary>
    public bool OfflineMode { get; set; }

    /// <summary>Priority for round-robin when multiple gateways match a BIN (lower = higher priority).</summary>
    public int Priority { get; set; } = 100;

    /// <summary>Optional description / notes.</summary>
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public List<GatewayBinRouteEntity> BinRoutes { get; set; } = [];
}

/// <summary>Protocol used by a gateway to communicate with the bank.</summary>
public enum GatewayProtocol
{
    /// <summary>ISO 8583 over raw TCP with STAN-correlated multiplexing.</summary>
    Iso8583 = 0,

    /// <summary>ISO 20022 over gRPC (bank's CardTransactionService).</summary>
    Iso20022Grpc = 1
}

/// <summary>
/// Maps a card BIN prefix to a specific gateway. When a transaction arrives,
/// the PAN's leading digits are matched against these routes to select the gateway.
/// A gateway with no BIN routes accepts all BINs (default/fallback gateway).
/// </summary>
public class GatewayBinRouteEntity
{
    public int Id { get; set; }

    public int GatewayId { get; set; }

    /// <summary>BIN prefix to match (e.g. "4", "41", "411234", "522"). Longer = more specific.</summary>
    public required string BinPrefix { get; set; }

    /// <summary>Optional label (e.g. "Visa Zimbabwe", "Mastercard SA").</summary>
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public GatewayEntity Gateway { get; set; } = null!;
}

/// <summary>
/// Audit log for gateway configuration changes.
/// </summary>
public class GatewayAuditLogEntity
{
    public int Id { get; set; }
    public int? GatewayId { get; set; }
    public required string Action { get; set; }
    public required string Details { get; set; }
    public string? PerformedBy { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
