namespace UniBank.SharedKernel.Events;

using UniBank.SharedKernel.Domain;

public sealed record UserRegistered(
    Guid UserId,
    string PhoneNumber,
    string FirstName,
    string LastName,
    string? Email) : DomainEvent;
