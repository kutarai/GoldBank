namespace UniBank.SharedKernel.Events;

using UniBank.SharedKernel.Domain;

public sealed record MerchantRegistered(
    Guid MerchantId,
    Guid OwnerAccountId,
    string MerchantCode,
    string BusinessName,
    bool IsAgent) : DomainEvent;
