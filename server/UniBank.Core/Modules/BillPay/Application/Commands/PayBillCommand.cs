namespace UniBank.Core.Modules.BillPay.Application.Commands;

/// <summary>
/// Command to pay a bill through a registered provider (STORY-038).
/// Requires PIN verification and sufficient account balance.
/// </summary>
public sealed record PayBillCommand(
    Guid AccountId,
    Guid ProviderId,
    string BillingReference,
    decimal Amount,
    string Currency,
    string Pin,
    string TenantId);
