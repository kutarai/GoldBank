using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.BillPay.Domain.Events;

/// <summary>
/// Domain event raised when a bill payment has been completed successfully (STORY-038).
/// </summary>
public sealed record BillPaymentCompleted(
    Guid PaymentId,
    Guid AccountId,
    Guid ProviderId,
    string ProviderName,
    decimal Amount,
    decimal Fee,
    string Currency,
    string Reference,
    string? Token,
    string BillingReference) : DomainEvent;
