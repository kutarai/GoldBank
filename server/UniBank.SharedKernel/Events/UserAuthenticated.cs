namespace UniBank.SharedKernel.Events;

using UniBank.SharedKernel.Domain;

public sealed record UserAuthenticated(
    Guid AccountId,
    string DeviceId) : DomainEvent;
