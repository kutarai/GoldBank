using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Transfers.Infrastructure.Services;

/// <summary>
/// Provides exchange rates between currency pairs for cross-border transfers (STORY-030).
/// Currently uses hardcoded rates for common African and major currency corridors.
/// Will be replaced with a live rate provider integration in a future sprint.
/// </summary>
public sealed class ExchangeRateService
{
    private static readonly Dictionary<string, decimal> Rates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ZWG/USD"] = 0.074m,
        ["USD/ZWG"] = 13.5m,
        ["ZWG/EUR"] = 0.068m,
        ["EUR/ZWG"] = 14.7m,
        ["ZWG/GBP"] = 0.058m,
        ["GBP/ZWG"] = 17.1m,
        ["ZWG/ZAR"] = 0.75m,
        ["ZAR/ZWG"] = 1.333m,
        ["USD/EUR"] = 0.92m,
        ["EUR/USD"] = 1.09m,
    };

    /// <summary>
    /// Gets the exchange rate and its inverse for a currency pair.
    /// Returns a failure result if the pair is not supported.
    /// </summary>
    public Result<(decimal Rate, decimal InverseRate)> GetRate(string fromCurrency, string toCurrency)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return Result.Success((1.0m, 1.0m));

        var key = $"{fromCurrency}/{toCurrency}";
        var inverseKey = $"{toCurrency}/{fromCurrency}";

        if (Rates.TryGetValue(key, out var rate))
        {
            var inverseRate = Rates.TryGetValue(inverseKey, out var inverse) ? inverse : Math.Round(1.0m / rate, 6);
            return Result.Success((rate, inverseRate));
        }

        // Try computing via inverse
        if (Rates.TryGetValue(inverseKey, out var inverseDirectRate))
        {
            var computedRate = Math.Round(1.0m / inverseDirectRate, 6);
            return Result.Success((computedRate, inverseDirectRate));
        }

        return Result.Failure<(decimal Rate, decimal InverseRate)>(
            new Error("Exchange.UnsupportedPair",
                $"Exchange rate not available for {fromCurrency}/{toCurrency}."));
    }

    /// <summary>
    /// Converts an amount from one currency to another using the current rate.
    /// Returns the converted amount and the rate used.
    /// </summary>
    public Result<(decimal ConvertedAmount, decimal Rate)> Convert(
        decimal amount, string fromCurrency, string toCurrency)
    {
        var rateResult = GetRate(fromCurrency, toCurrency);
        if (rateResult.IsFailure)
            return Result.Failure<(decimal ConvertedAmount, decimal Rate)>(rateResult.Error);

        var (rate, _) = rateResult.Value;
        var convertedAmount = Math.Round(amount * rate, 2);
        return Result.Success((convertedAmount, rate));
    }
}
