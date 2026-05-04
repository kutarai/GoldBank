namespace GoldBank.Core.Modules.Agents.Infrastructure.Services;

/// <summary>
/// Unified tariff engine that calculates customer fees, agent/merchant commissions,
/// and IMTT tax for all transaction types (EPIC-020, STORY-084).
///
/// Tariff schedule:
///   Cash-In:  Customer fee 1.0% (min $0.50), Agent commission 1.5%/1.0%, IMTT 2%
///   Cash-Out: Customer fee 1.5% (min $1.00), Agent commission 2.0%/1.5%, IMTT 2%
///   POS NFC:  Customer fee 0.5%, Merchant discount 1.5%, IMTT 2%
///   POS QR:   Customer fee 0.3%, Merchant discount 1.0%, IMTT 2%
/// </summary>
public sealed class TariffEngine
{
    // IMTT - Intermediated Money Transfer Tax (ZIMRA)
    private const decimal ImttRate = 0.02m;

    // Customer fee rates
    private const decimal CashInCustomerFeeRate = 0.01m;      // 1.0%
    private const decimal CashInCustomerFeeMin = 0.50m;
    private const decimal CashOutCustomerFeeRate = 0.015m;     // 1.5%
    private const decimal CashOutCustomerFeeMin = 1.00m;
    private const decimal NfcCustomerFeeRate = 0.005m;         // 0.5%
    private const decimal QrCustomerFeeRate = 0.003m;          // 0.3%

    // Agent commission rates (tiered)
    private const decimal CashInAgentBaseRate = 0.015m;        // 1.5%
    private const decimal CashOutAgentBaseRate = 0.02m;        // 2.0%
    private const decimal AgentTierDiscount = 0.005m;          // 0.5% discount above threshold
    private const decimal AgentHighValueThreshold = 10_000m;

    // Merchant discount rates (charged TO merchant)
    private const decimal NfcMerchantDiscountRate = 0.015m;    // 1.5%
    private const decimal QrMerchantDiscountRate = 0.01m;      // 1.0%

    /// <summary>
    /// Calculates the full tariff breakdown for a transaction.
    /// </summary>
    public TariffBreakdown Calculate(string transactionType, decimal amount)
    {
        var (customerFeeRate, customerFeeMin) = GetCustomerFeeParams(transactionType);
        var commissionRate = GetCommissionRate(transactionType, amount);
        var merchantDiscountRate = GetMerchantDiscountRate(transactionType);

        var customerFee = Math.Max(Math.Round(amount * customerFeeRate, 2), customerFeeMin);
        var tax = Math.Round(amount * ImttRate, 2);
        var agentCommission = Math.Round(amount * commissionRate, 2);
        var merchantDiscount = Math.Round(amount * merchantDiscountRate, 2);

        var isAgentTransaction = transactionType is "cash_in" or "cash_out";
        var isMerchantTransaction = transactionType is "pos_nfc" or "pos_qr";

        return new TariffBreakdown(
            TransactionType: transactionType,
            Amount: amount,
            CustomerFee: customerFee,
            CustomerFeeRate: customerFeeRate,
            Tax: tax,
            TaxRate: ImttRate,
            AgentCommission: isAgentTransaction ? agentCommission : 0m,
            AgentCommissionRate: isAgentTransaction ? commissionRate : 0m,
            MerchantDiscount: isMerchantTransaction ? merchantDiscount : 0m,
            MerchantDiscountRate: isMerchantTransaction ? merchantDiscountRate : 0m,
            TotalCustomerDebit: amount + customerFee + tax,
            MerchantCredit: isMerchantTransaction ? amount - merchantDiscount : 0m);
    }

    private static (decimal Rate, decimal Min) GetCustomerFeeParams(string transactionType) => transactionType switch
    {
        "cash_in" => (CashInCustomerFeeRate, CashInCustomerFeeMin),
        "cash_out" => (CashOutCustomerFeeRate, CashOutCustomerFeeMin),
        "pos_nfc" => (NfcCustomerFeeRate, 0m),
        "pos_qr" => (QrCustomerFeeRate, 0m),
        _ => (0m, 0m)
    };

    private static decimal GetCommissionRate(string transactionType, decimal amount)
    {
        var baseRate = transactionType switch
        {
            "cash_in" => CashInAgentBaseRate,
            "cash_out" => CashOutAgentBaseRate,
            _ => 0m
        };

        if (baseRate > 0m && amount > AgentHighValueThreshold)
            baseRate -= AgentTierDiscount;

        return Math.Max(baseRate, 0m);
    }

    private static decimal GetMerchantDiscountRate(string transactionType) => transactionType switch
    {
        "pos_nfc" => NfcMerchantDiscountRate,
        "pos_qr" => QrMerchantDiscountRate,
        _ => 0m
    };
}

/// <summary>
/// Complete fee, commission, and tax breakdown for a transaction.
/// </summary>
public sealed record TariffBreakdown(
    string TransactionType,
    decimal Amount,
    decimal CustomerFee,
    decimal CustomerFeeRate,
    decimal Tax,
    decimal TaxRate,
    decimal AgentCommission,
    decimal AgentCommissionRate,
    decimal MerchantDiscount,
    decimal MerchantDiscountRate,
    decimal TotalCustomerDebit,
    decimal MerchantCredit);
