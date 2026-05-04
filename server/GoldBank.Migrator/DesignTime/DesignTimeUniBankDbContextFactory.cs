using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using GoldBank.Core.Common.Persistence;

namespace GoldBank.Migrator.DesignTime;

/// <summary>
/// Factory used by <c>dotnet ef</c> tooling to create a design-time <see cref="GoldBankDbContext"/>.
/// Uses a fixed "bank" schema so that the generated snapshot is schema-agnostic;
/// at runtime, the actual tenant schema is provided via the CLI or DI.
///
/// Usage:
///   dotnet ef migrations add Initial --context GoldBankDbContext --output-dir Migrations/GoldBankDb
///   dotnet ef database update --context GoldBankDbContext
/// </summary>
internal sealed class DesignTimeGoldBankDbContextFactory : IDesignTimeDbContextFactory<GoldBankDbContext>
{
    public GoldBankDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables("GOLDBANK_")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        var optionsBuilder = new DbContextOptionsBuilder<GoldBankDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly("GoldBank.Migrator");
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "bank");
        });

        return new GoldBankDbContext(optionsBuilder.Options, "bank");
    }
}
