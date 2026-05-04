namespace GoldBank.SharedKernel.Events;

using GoldBank.SharedKernel.Domain;

public sealed record KYCRejected(
    Guid UserId,
    Guid? AccountId,
    string Reason,
    string? RejectedField) : DomainEvent;
