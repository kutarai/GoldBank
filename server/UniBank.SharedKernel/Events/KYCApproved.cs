namespace UniBank.SharedKernel.Events;

using UniBank.SharedKernel.Domain;

public sealed record KYCApproved(
    Guid UserId,
    Guid AccountId,
    string VerificationLevel) : DomainEvent;
