namespace UniBank.TerminalManager.Domain;

/// <summary>
/// Tracks software/firmware/config updates pushed to terminals (STORY-046).
/// </summary>
public sealed class TerminalUpdate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TerminalId { get; set; }
    public string UpdateType { get; set; } = default!;
    public string Version { get; set; } = default!;
    public string Status { get; set; } = "pending";
    public DateTime PushedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AppliedAt { get; set; }
}
