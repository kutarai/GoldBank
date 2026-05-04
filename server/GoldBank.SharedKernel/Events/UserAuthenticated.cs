namespace GoldBank.SharedKernel.Events;

using GoldBank.SharedKernel.Domain;

public sealed record UserAuthenticated(
    Guid AccountId,
    string DeviceId) : DomainEvent;
