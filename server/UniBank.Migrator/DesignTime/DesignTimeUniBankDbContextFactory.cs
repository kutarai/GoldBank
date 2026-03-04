using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using UniBank.Core.Common.Persistence;

namespace UniBank.Migrator.DesignTime;

/// <summary>
/// Factory used by <c>dotnet ef</c> tooling to create a design-time <see cref="UniBankDbContext"/>.
/// Uses a fixed "bank" schema so that the generated snapshot is schema-agnostic;
/// at runtime, the actual tenant schema is provided via the CLI or DI.
///
/// Usage:
///   dotnet ef migrations add Initial --context UniBankDbContext --output-dir Migrations/UniBankDb
///   dotnet ef database update --context UniBankDbContext
/// </summary>
internal sealed class DesignTimeUniBankDbContextFactory : IDesignTimeDbContextFactory<UniBankDbContext>
{
    public UniBankDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables("UNIBANK_")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        var optionsBuilder = new DbContextOptionsBuilder<UniBankDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly("UniBank.Migrator");
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "bank");
        });

        return new UniBankDbContext(optionsBuilder.Options, "bank");
    }
}
