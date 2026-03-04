namespace UniBank.TerminalManager.Domain;

/// <summary>
/// Terminal entity representing a registered EFT POS terminal (STORY-046).
/// Managed independently from Core DbContext with its own schema isolation.
/// </summary>
public sealed class Terminal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MerchantId { get; set; }
    public string TenantId { get; set; } = default!;
    public string SerialNumber { get; set; } = default!;
    public string Model { get; set; } = default!;
    public string FirmwareVersion { get; set; } = default!;
    public string Status { get; set; } = "inactive";
    public string? Location { get; set; }
    public string MqttTopicPrefix { get; set; } = default!;
    public string? IpAddress { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public DateTime? LastKeyInjection { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ActivatedAt { get; set; }
}
