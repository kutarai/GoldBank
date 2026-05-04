namespace GoldBank.Core.Modules.Agents.Infrastructure.Services;

/// <summary>
/// Calculates agent commissions based on transaction type and amount (STORY-034).
/// Applies tiered commission rates: base rates drop by 0.5% for amounts above 10,000.
/// </summary>
public sealed class CommissionEngine
{
    private const decimal CashInBaseRate = 0.015m;   // 1.5%
    private const decimal CashOutBaseRate = 0.02m;   // 2.0%
    private const decimal TierDiscount = 0.005m;     // 0.5% discount for high-value transactions
    private const decimal HighValueThreshold = 10_000m;

    /// <summary>
    /// Calculates the commission rate and amount for a given transaction.
    /// </summary>
    /// <param name="transactionType">The transaction type: "cash_in" or "cash_out".</param>
    /// <param name="amount">The transaction amount.</param>
    /// <returns>A tuple of (rate, commissionAmount) where rate is the applied percentage.</returns>
    public (decimal Rate, decimal CommissionAmount) CalculateCommission(string transactionType, decimal amount)
    {
        var baseRate = transactionType switch
        {
            "cash_in" => CashInBaseRate,
            "cash_out" => CashOutBaseRate,
            _ => 0m
        };

        var effectiveRate = amount > HighValueThreshold
            ? baseRate - TierDiscount
            : baseRate;

        // Ensure rate never goes negative
        if (effectiveRate < 0m)
            effectiveRate = 0m;

        var commissionAmount = Math.Round(amount * effectiveRate, 2);

        return (effectiveRate, commissionAmount);
    }
}
