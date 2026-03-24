using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Domain.Entities;

namespace UniBank.Core.Modules.Loans.Infrastructure.Services;

/// <summary>
/// Calculates a credit score (0-1000) for loan applicants based on account history,
/// KYC level, transaction volume, existing loan performance, and balance average.
/// </summary>
public sealed class CreditScoringEngine
{
    private readonly UniBankDbContext _dbContext;

    public CreditScoringEngine(UniBankDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Calculate a credit score for the given account.
    /// Score breakdown: account age (200), KYC level (200), transaction volume 90d (300),
    /// existing loan performance (200), balance average (100). Total: 1000.
    /// </summary>
    public async Task<int> CalculateScoreAsync(Account account, CancellationToken cancellationToken = default)
    {
        var score = 0;

        // 1. Account age score (max 200 points)
        var accountAgeDays = (DateTime.UtcNow - account.CreatedAt).TotalDays;
        score += accountAgeDays switch
        {
            >= 365 => 200,
            >= 180 => 150,
            >= 90 => 100,
            >= 30 => 50,
            _ => 20,
        };

        // 2. KYC level score (max 200 points)
        score += account.KycLevel switch
        {
            >= 3 => 200,
            2 => 150,
            1 => 100,
            _ => 0,
        };

        // 3. Transaction volume in last 90 days (max 300 points)
        var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);
        var transactionStats = await _dbContext.Transactions
            .Where(t => t.AccountId == account.Id && t.CreatedAt >= ninetyDaysAgo && t.Status == "completed")
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Volume = g.Sum(t => Math.Abs(t.Amount)) })
            .FirstOrDefaultAsync(cancellationToken);

        if (transactionStats is not null)
        {
            // Count-based (max 150)
            score += transactionStats.Count switch
            {
                >= 50 => 150,
                >= 20 => 100,
                >= 10 => 60,
                >= 5 => 30,
                _ => 10,
            };

            // Volume-based (max 150)
            score += transactionStats.Volume switch
            {
                >= 10000m => 150,
                >= 5000m => 100,
                >= 1000m => 60,
                >= 500m => 30,
                _ => 10,
            };
        }

        // 4. Existing loan performance (max 200 points)
        var existingLoans = await _dbContext.Loans
            .Where(l => l.AccountId == account.Id && l.DeletedAt == null)
            .ToListAsync(cancellationToken);

        if (existingLoans.Count == 0)
        {
            // No loan history — neutral score
            score += 100;
        }
        else
        {
            var paidOff = existingLoans.Count(l => l.Status == "paid_off");
            var defaulted = existingLoans.Count(l => l.Status == "defaulted");

            if (defaulted > 0)
                score += 0;
            else if (paidOff > 0)
                score += Math.Min(200, 100 + paidOff * 50);
            else
                score += 80; // Active loans, no defaults
        }

        // 5. Balance average (max 100 points)
        score += account.Balance switch
        {
            >= 5000m => 100,
            >= 2000m => 70,
            >= 500m => 40,
            >= 100m => 20,
            _ => 5,
        };

        return Math.Min(1000, score);
    }

    /// <summary>
    /// Determine interest rate based on credit score and tenure.
    /// Reads rate matrix from SystemConfig (key: "loan.rate_matrix").
    /// Falls back to hardcoded defaults if not configured.
    /// </summary>
    public async Task<decimal> GetInterestRateAsync(int creditScore, int tenureMonths, string? tenantId = null, CancellationToken ct = default)
    {
        var matrix = await LoadRateMatrixAsync(tenantId, ct);
        if (matrix is { Count: > 0 })
        {
            var tier = matrix
                .Where(t => creditScore >= t.ScoreMin && creditScore <= t.ScoreMax)
                .FirstOrDefault();

            if (tier is not null)
            {
                var rate = tenureMonths switch
                {
                    <= 6 => tier.Tenure6,
                    <= 12 => tier.Tenure12,
                    <= 24 => tier.Tenure24,
                    <= 36 => tier.Tenure36,
                    _ => tier.Tenure48,
                };
                return rate / 100m; // stored as percentage, convert to decimal
            }
        }

        // Fallback to hardcoded defaults
        return GetInterestRateDefault(creditScore);
    }

    /// <summary>Static fallback when SystemConfig is not available.</summary>
    public static decimal GetInterestRateDefault(int creditScore)
    {
        return creditScore switch
        {
            >= 800 => 0.18m,
            >= 650 => 0.22m,
            >= 500 => 0.26m,
            >= 350 => 0.30m,
            _ => 0.36m,
        };
    }

    private async Task<List<RateMatrixTier>?> LoadRateMatrixAsync(string? tenantId, CancellationToken ct)
    {
        try
        {
            var config = await _dbContext.SystemConfigs
                .Where(c => c.Key == "loan.rate_matrix")
                .Where(c => c.TenantId == tenantId || c.TenantId == null)
                .OrderByDescending(c => c.TenantId)
                .FirstOrDefaultAsync(ct);

            if (config is null || string.IsNullOrWhiteSpace(config.ValueJson))
                return null;

            return JsonSerializer.Deserialize<List<RateMatrixTier>>(config.ValueJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Rate matrix tier matching the admin portal's configuration format.</summary>
    private sealed class RateMatrixTier
    {
        public int ScoreMin { get; set; }
        public int ScoreMax { get; set; }
        public string Label { get; set; } = "";
        public decimal Tenure6 { get; set; }
        public decimal Tenure12 { get; set; }
        public decimal Tenure24 { get; set; }
        public decimal Tenure36 { get; set; }
        public decimal Tenure48 { get; set; }
    }

    /// <summary>
    /// Calculate monthly payment using standard amortization formula.
    /// M = P * [r(1+r)^n] / [(1+r)^n - 1]
    /// </summary>
    public static decimal CalculateMonthlyPayment(decimal principal, decimal annualRate, int tenureMonths)
    {
        var monthlyRate = annualRate / 12m;

        if (monthlyRate == 0)
            return Math.Round(principal / tenureMonths, 2);

        var factor = (decimal)Math.Pow((double)(1 + monthlyRate), tenureMonths);
        var payment = principal * (monthlyRate * factor) / (factor - 1);
        return Math.Round(payment, 2);
    }
}
