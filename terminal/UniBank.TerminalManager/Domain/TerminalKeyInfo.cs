namespace UniBank.TerminalManager.Domain;

/// <summary>
/// Key metadata for terminal encryption key lifecycle (STORY-047).
/// Tracks master and session keys managed through the HSM.
/// </summary>
public sealed class TerminalKeyInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TerminalId { get; set; }
    public string MasterKeyId { get; set; } = default!;
    public string? ActiveSessionKeyId { get; set; }
    public DateTime LastRotation { get; set; } = DateTime.UtcNow;
    public DateTime NextRotation { get; set; }
}
