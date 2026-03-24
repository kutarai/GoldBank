using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UniBank.Core.Common.Persistence;

namespace UniBank.Migrator;

/// <summary>
/// Standalone migration runner for UniBank server DbContexts.
///
/// Usage:
///   dotnet run                              (applies all migrations)
///   dotnet run -- --context PublicDb         (PublicDbContext only)
///   dotnet run -- --context UniBankDb        (UniBankDbContext only)
///   dotnet run -- --demo                    (apply migrations + seed demo data)
///
/// Design-time (dotnet ef):
///   dotnet ef migrations add Initial --context UniBankDbContext --output-dir Migrations/UniBankDb
///   dotnet ef migrations add Initial --context PublicDbContext --output-dir Migrations/PublicDb
/// </summary>
internal sealed class Program
{
    private static async Task<int> Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables("UNIBANK_")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        var context = GetArg(args, "--context");
        var seed = args.Contains("--demo", StringComparer.OrdinalIgnoreCase);

        switch (context)
        {
            case "PublicDb":
                await MigratePublicAsync(connectionString);
                break;

            case "UniBankDb":
                await MigrateUniBankAsync(connectionString);
                if (seed) await RunSeedAsync(connectionString);
                break;

            case null:
                // No --context specified: apply all migrations (PublicDb first, then UniBankDb)
                await MigratePublicAsync(connectionString);
                await MigrateUniBankAsync(connectionString);
                if (seed) await RunSeedAsync(connectionString);
                break;

            default:
                Console.Error.WriteLine($"Unknown context '{context}'. Use 'UniBankDb', 'PublicDb', or omit for all.");
                return 1;
        }

        return 0;
    }

    private static async Task RunSeedAsync(string connectionString)
    {
        Console.WriteLine("Running DemoSeeder...");

        var options = new DbContextOptionsBuilder<UniBankDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly("UniBank.Migrator");
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "bank");
            })
            .Options;

        await using var db = new UniBankDbContext(options, "bank");
        await DemoSeeder.SeedAsync(db);
    }

    private static async Task MigratePublicAsync(string connectionString)
    {
        Console.WriteLine("Applying PublicDbContext migrations...");

        var options = new DbContextOptionsBuilder<PublicDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly("UniBank.Migrator");
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Public", "bank");
            })
            .Options;

        await using var db = new PublicDbContext(options);
        await db.Database.MigrateAsync();

        Console.WriteLine("PublicDbContext migrations applied successfully.");
    }

    private static async Task MigrateUniBankAsync(string connectionString)
    {
        const string migrationsSchema = "bank";
        Console.WriteLine($"Applying UniBankDbContext migrations to schema '{migrationsSchema}'...");

        var options = new DbContextOptionsBuilder<UniBankDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly("UniBank.Migrator");
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", migrationsSchema);
            })
            .Options;

        await using var db = new UniBankDbContext(options, migrationsSchema);
        await db.Database.MigrateAsync();

        Console.WriteLine($"UniBankDbContext migrations applied to schema '{migrationsSchema}'.");
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
