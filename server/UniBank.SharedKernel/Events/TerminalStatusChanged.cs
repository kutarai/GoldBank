namespace UniBank.SharedKernel.Events;

using UniBank.SharedKernel.Domain;

public sealed record TerminalStatusChanged(
    string TerminalId,
    string PreviousStatus,
    string NewStatus,
    string? Reason) : DomainEvent;
