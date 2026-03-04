namespace UniBank.SharedKernel.Events;

using UniBank.SharedKernel.Domain;

public sealed record KYCRejected(
    Guid UserId,
    Guid? AccountId,
    string Reason,
    string? RejectedField) : DomainEvent;
