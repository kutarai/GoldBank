using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.Transfers.Domain.Events;

/// <summary>
/// Domain event raised when a P2P or cross-border transfer is completed (STORY-031).
/// Used to trigger notifications and downstream processing.
/// </summary>
public sealed record TransferCompletedEvent(
    Guid TransferId,
    Guid SenderAccountId,
    string RecipientPhone,
    decimal Amount,
    string Currency,
    string Type) : DomainEvent;
