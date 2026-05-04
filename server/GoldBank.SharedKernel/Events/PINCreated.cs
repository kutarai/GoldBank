namespace GoldBank.SharedKernel.Events;

using GoldBank.SharedKernel.Domain;

public sealed record PINCreated(
    Guid UserId,
    Guid AccountId,
    string PINType) : DomainEvent;
