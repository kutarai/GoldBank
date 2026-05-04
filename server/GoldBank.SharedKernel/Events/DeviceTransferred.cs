namespace GoldBank.SharedKernel.Events;

using GoldBank.SharedKernel.Domain;

public sealed record DeviceTransferred(
    Guid AccountId,
    string OldDeviceId,
    string NewDeviceId) : DomainEvent;
