namespace GoldBank.Core.Modules.Loans.Application.Handlers;

/// <summary>
/// Result of a loan application, containing approval details, terms, and credit score.
/// </summary>
public sealed record LoanApplicationResult(
    string LoanId,
    string Reference,
    string Status,
    decimal Principal,
    string Currency,
    decimal InterestRate,
    decimal MonthlyPayment,
    int TenureMonths,
    int CreditScore,
    decimal NewBalance);
