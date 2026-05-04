namespace GoldBank.SharedKernel.Events;

using GoldBank.SharedKernel.Domain;

public sealed record KYCApproved(
    Guid UserId,
    Guid AccountId,
    string VerificationLevel) : DomainEvent;
