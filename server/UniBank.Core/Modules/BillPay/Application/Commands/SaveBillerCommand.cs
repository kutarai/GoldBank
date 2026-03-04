namespace UniBank.Core.Modules.BillPay.Application.Commands;

/// <summary>
/// Command to save a biller as a favourite for quick repeat payments (STORY-039).
/// </summary>
public sealed record SaveBillerCommand(
    Guid AccountId,
    Guid ProviderId,
    string BillingReference,
    string Nickname,
    string TenantId);
