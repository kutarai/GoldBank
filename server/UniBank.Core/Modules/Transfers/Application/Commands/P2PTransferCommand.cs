namespace UniBank.Core.Modules.Transfers.Application.Commands;

/// <summary>
/// Command to initiate a domestic P2P transfer to a recipient identified by phone number (STORY-029).
/// The sender must be active, have a valid PIN, and sufficient balance to cover amount + fee.
/// </summary>
public sealed record P2PTransferCommand(
    Guid SenderAccountId,
    string RecipientPhone,
    decimal Amount,
    string Currency,
    string? Description,
    string Pin,
    string TenantId);
