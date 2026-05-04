namespace GoldBank.SharedKernel.Events;

using GoldBank.SharedKernel.Domain;

public sealed record TransactionFailed(
    Guid TransactionId,
    Guid SourceAccountId,
    decimal Amount,
    string Currency,
    string Reason,
    string ErrorCode) : DomainEvent;
