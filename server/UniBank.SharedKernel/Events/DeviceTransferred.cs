namespace UniBank.SharedKernel.Events;

using UniBank.SharedKernel.Domain;

public sealed record DeviceTransferred(
    Guid AccountId,
    string OldDeviceId,
    string NewDeviceId) : DomainEvent;
