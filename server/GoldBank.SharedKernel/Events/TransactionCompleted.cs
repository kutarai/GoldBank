namespace GoldBank.SharedKernel.Events;

using GoldBank.SharedKernel.Domain;

public sealed record TransactionCompleted(
    Guid TransactionId,
    Guid SourceAccountId,
    Guid? DestinationAccountId,
    decimal Amount,
    string Currency,
    string TransactionType,
    string? ReferenceNumber) : DomainEvent;
