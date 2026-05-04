using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.Accounts.Domain.Entities;

/// <summary>
/// Tracks a device transfer request (STORY-014).
/// When a user moves to a new device, they must verify via OTP + PIN.
/// </summary>
public class DeviceTransferRequest : AggregateRoot
{
    public Guid AccountId { get; set; }
    public string TransferReference { get; set; } = default!;
    public string OldDeviceId { get; set; } = default!;
    public string NewDeviceId { get; set; } = default!;
    public string Status { get; set; } = "pending";
    public string TenantId { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
