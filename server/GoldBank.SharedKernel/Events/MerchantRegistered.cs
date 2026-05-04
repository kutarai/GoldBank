namespace GoldBank.SharedKernel.Events;

using GoldBank.SharedKernel.Domain;

public sealed record MerchantRegistered(
    Guid MerchantId,
    Guid OwnerAccountId,
    string MerchantCode,
    string BusinessName,
    bool IsAgent) : DomainEvent;
