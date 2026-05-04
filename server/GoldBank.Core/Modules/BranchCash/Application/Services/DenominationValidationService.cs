using Microsoft.EntityFrameworkCore;
using GoldBank.Core.Common.Persistence;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.BranchCash.Application.Services;

/// <summary>
/// Validates that a denomination breakdown sums to the requested amount AND
/// uses only currently registered active denominations for the currency
/// (STORY-151, table-backed since STORY-163).
/// </summary>
public sealed class DenominationValidationService
{
    private readonly GoldBankDbContext _db;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly object _lock = new();
    private static Dictionary<string, HashSet<decimal>>? _cache;
    private static DateTime _cacheLoadedAt = DateTime.MinValue;

    public DenominationValidationService(GoldBankDbContext db)
    {
        _db = db;
    }

    public Result<DenominationValidationResult> Validate(
        string currency,
        decimal totalAmount,
        IReadOnlyList<DenominationLine> breakdown)
    {
        if (breakdown == null || breakdown.Count == 0)
            return Result.Failure<DenominationValidationResult>(DenominationErrors.EmptyBreakdown);

        var registry = LoadRegistry();
        if (!registry.TryGetValue(currency, out var validFaceValues))
            return Result.Failure<DenominationValidationResult>(DenominationErrors.UnknownCurrency);

        if (breakdown.All(b => b.Count == 0))
            return Result.Failure<DenominationValidationResult>(DenominationErrors.EmptyBreakdown);

        decimal computed = 0m;
        var noteCount = 0;
        var coinCount = 0;
        var totalPieces = 0;

        foreach (var line in breakdown)
        {
            if (line.Count < 0)
                return Result.Failure<DenominationValidationResult>(DenominationErrors.NegativeCount);

            if (!validFaceValues.Contains(line.FaceValue))
                return Result.Failure<DenominationValidationResult>(DenominationErrors.UnknownDenomination);

            computed += line.FaceValue * line.Count;
            totalPieces += line.Count;

            if (string.Equals(line.Type, "Note", StringComparison.OrdinalIgnoreCase))
                noteCount += line.Count;
            else if (string.Equals(line.Type, "Coin", StringComparison.OrdinalIgnoreCase))
                coinCount += line.Count;
        }

        if (Math.Round(computed, 2) != Math.Round(totalAmount, 2))
            return Result.Failure<DenominationValidationResult>(DenominationErrors.SumMismatch);

        return Result.Success(new DenominationValidationResult(
            ComputedTotal: Math.Round(computed, 2),
            TotalPieceCount: totalPieces,
            NoteCount: noteCount,
            CoinCount: coinCount));
    }

    /// <summary>Force refresh on the next call (e.g. after admin edits).</summary>
    public static void InvalidateCache()
    {
        lock (_lock) { _cache = null; _cacheLoadedAt = DateTime.MinValue; }
    }

    private Dictionary<string, HashSet<decimal>> LoadRegistry()
    {
        lock (_lock)
        {
            if (_cache != null && (DateTime.UtcNow - _cacheLoadedAt) < CacheTtl)
                return _cache;

            var rows = _db.CurrencyDenominations
                .AsNoTracking()
                .Where(c => c.IsActive)
                .Select(c => new { c.Currency, c.FaceValue })
                .ToList();

            _cache = rows
                .GroupBy(r => r.Currency, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(r => r.FaceValue).ToHashSet(),
                    StringComparer.OrdinalIgnoreCase);
            _cacheLoadedAt = DateTime.UtcNow;
            return _cache;
        }
    }
}

public sealed record DenominationLine(decimal FaceValue, int Count, string? Type);

public sealed record DenominationValidationResult(
    decimal ComputedTotal,
    int TotalPieceCount,
    int NoteCount,
    int CoinCount);

public static class DenominationErrors
{
    public static readonly Error SumMismatch         = new("Denom.SumMismatch", "Denomination breakdown does not sum to the total amount.");
    public static readonly Error UnknownDenomination = new("Denom.Unknown", "One or more denominations are not registered for this currency.");
    public static readonly Error UnknownCurrency     = new("Denom.UnknownCurrency", "Currency is not registered.");
    public static readonly Error InactiveDenomination = new("Denom.Inactive", "One or more denominations are inactive.");
    public static readonly Error NegativeCount       = new("Denom.NegativeCount", "Denomination counts must be non-negative.");
    public static readonly Error EmptyBreakdown      = new("Denom.Empty", "Denomination breakdown is empty.");
}
