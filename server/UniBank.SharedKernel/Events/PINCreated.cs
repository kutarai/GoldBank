namespace UniBank.SharedKernel.Events;

using UniBank.SharedKernel.Domain;

public sealed record PINCreated(
    Guid UserId,
    Guid AccountId,
    string PINType) : DomainEvent;
