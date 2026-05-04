namespace GoldBank.Core.Modules.Loans.Application.Commands;

/// <summary>
/// Command to apply for a personal unsecured loan.
/// The applicant must have an active account, valid PIN, and KYC level >= 1.
/// </summary>
public sealed record ApplyForLoanCommand(
    Guid AccountId,
    decimal Amount,
    string Currency,
    int TenureMonths,
    string Purpose,
    string Pin,
    string TenantId,
    IReadOnlyList<Guid> CollateralAssetIds);
