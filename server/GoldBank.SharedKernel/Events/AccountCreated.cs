namespace GoldBank.SharedKernel.Events;

using GoldBank.SharedKernel.Domain;

public sealed record AccountCreated(
    Guid AccountId,
    Guid UserId,
    string PhoneNumber,
    string AccountType,
    string Currency) : DomainEvent;
