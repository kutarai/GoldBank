namespace GoldBank.SharedKernel.Events;

using GoldBank.SharedKernel.Domain;

public sealed record TerminalStatusChanged(
    string TerminalId,
    string PreviousStatus,
    string NewStatus,
    string? Reason) : DomainEvent;
