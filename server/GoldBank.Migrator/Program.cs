using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Ekub.Domain.Entities;

namespace GoldBank.Migrator;

/// <summary>
/// Standalone migration runner for GoldBank server DbContexts.
///
/// Usage:
///   dotnet run                              (applies all migrations)
///   dotnet run -- --context PublicDb         (PublicDbContext only)
///   dotnet run -- --context GoldBankDb        (GoldBankDbContext only)
///   dotnet run -- --demo                    (apply migrations + seed demo data)
///   dotnet run -- --apply-ekub-fees         (apply Ekub monthly bank fee for current month)
///   dotnet run -- --apply-ekub-fees --period 2026-05
///
/// Design-time (dotnet ef):
///   dotnet ef migrations add Initial --context GoldBankDbContext --output-dir Migrations/GoldBankDb
///   dotnet ef migrations add Initial --context PublicDbContext --output-dir Migrations/PublicDb
/// </summary>
internal sealed class Program
{
    private static async Task<int> Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables("GOLDBANK_")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        var context = GetArg(args, "--context");
        var seed = args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        var applyEkubFees = args.Contains("--apply-ekub-fees", StringComparer.OrdinalIgnoreCase);

        if (applyEkubFees)
        {
            var period = GetArg(args, "--period") ?? DateTime.UtcNow.ToString("yyyy-MM");
            return await ApplyEkubMonthlyFeesAsync(connectionString, period);
        }

        switch (context)
        {
            case "PublicDb":
                await MigratePublicAsync(connectionString);
                break;

            case "GoldBankDb":
                await MigrateGoldBankAsync(connectionString);
                if (seed) await RunSeedAsync(connectionString);
                break;

            case null:
                // No --context specified: apply all migrations (PublicDb first, then GoldBankDb)
                await MigratePublicAsync(connectionString);
                await MigrateGoldBankAsync(connectionString);
                if (seed) await RunSeedAsync(connectionString);
                break;

            default:
                Console.Error.WriteLine($"Unknown context '{context}'. Use 'GoldBankDb', 'PublicDb', or omit for all.");
                return 1;
        }

        return 0;
    }

    private static async Task RunSeedAsync(string connectionString)
    {
        Console.WriteLine("Running DemoSeeder...");

        var options = new DbContextOptionsBuilder<GoldBankDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly("GoldBank.Migrator");
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "bank");
            })
            .Options;

        await using var db = new GoldBankDbContext(options, "bank");
        await DemoSeeder.SeedAsync(db);
    }

    private static async Task MigratePublicAsync(string connectionString)
    {
        Console.WriteLine("Applying PublicDbContext migrations...");

        var options = new DbContextOptionsBuilder<PublicDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly("GoldBank.Migrator");
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Public", "bank");
            })
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var db = new PublicDbContext(options);
        await db.Database.MigrateAsync();

        Console.WriteLine("PublicDbContext migrations applied successfully.");
    }

    private static async Task MigrateGoldBankAsync(string connectionString)
    {
        const string migrationsSchema = "bank";
        Console.WriteLine($"Applying GoldBankDbContext migrations to schema '{migrationsSchema}'...");

        var options = new DbContextOptionsBuilder<GoldBankDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly("GoldBank.Migrator");
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", migrationsSchema);
            })
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var db = new GoldBankDbContext(options, migrationsSchema);
        await db.Database.MigrateAsync();

        Console.WriteLine($"GoldBankDbContext migrations applied to schema '{migrationsSchema}'.");
    }

    /// <summary>
    /// Applies the Ekub monthly bank fee for <paramref name="period"/> against every Active group.
    /// Idempotent — relies on the unique (group_id, period) index on bank.ekub_fees so re-runs are safe.
    /// </summary>
    private static async Task<int> ApplyEkubMonthlyFeesAsync(string connectionString, string period)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(period, @"^\d{4}-\d{2}$"))
        {
            Console.Error.WriteLine($"Invalid period '{period}'. Expected 'YYYY-MM'.");
            return 2;
        }

        var options = new DbContextOptionsBuilder<GoldBankDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly("GoldBank.Migrator");
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "bank");
            })
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var db = new GoldBankDbContext(options, "bank");

        // Pull the per-currency fee from system_configs. Stored as JSON string, e.g. "50.00".
        var configs = await db.SystemConfigs
            .Where(c => c.Key == "ekub.monthly_fee_zwg" || c.Key == "ekub.monthly_fee_usd")
            .ToDictionaryAsync(c => c.Key, c => c.ValueJson);

        decimal FeeFor(string currency)
        {
            var key = $"ekub.monthly_fee_{currency.ToLowerInvariant()}";
            if (!configs.TryGetValue(key, out var json) || string.IsNullOrWhiteSpace(json)) return 0m;
            var trimmed = json.Trim('"');
            return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;
        }

        var groups = await db.EkubGroups
            .Where(g => g.Status == EkubGroupStatus.Active)
            .ToListAsync();

        var processed = 0;
        var recorded = 0;
        var skipped = 0;
        var now = DateTime.UtcNow;

        foreach (var g in groups)
        {
            processed++;

            var alreadyRecorded = await db.EkubFees
                .AnyAsync(f => f.GroupId == g.Id && f.Period == period);
            if (alreadyRecorded) { skipped++; continue; }

            var fee = FeeFor(g.Currency);
            if (fee <= 0) { skipped++; continue; }

            db.EkubFees.Add(new EkubFee
            {
                GroupId = g.Id,
                Period = period,
                Amount = fee,
                Currency = g.Currency,
                TenantId = g.TenantId,
                CreatedAt = now,
                UpdatedAt = now,
            });
            g.LastFeeAppliedAt = now;
            g.UpdatedAt = now;
            recorded++;
        }

        await db.SaveChangesAsync();
        Console.WriteLine($"[ekub-fees] period={period} processed={processed} recorded={recorded} skipped={skipped}");
        return 0;
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }
}
