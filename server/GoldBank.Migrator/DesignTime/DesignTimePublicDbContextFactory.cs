using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using GoldBank.Core.Common.Persistence;

namespace GoldBank.Migrator.DesignTime;

/// <summary>
/// Factory used by <c>dotnet ef</c> tooling to create a design-time <see cref="PublicDbContext"/>.
///
/// Usage:
///   dotnet ef migrations add Initial --context PublicDbContext --output-dir Migrations/PublicDb
///   dotnet ef database update --context PublicDbContext
/// </summary>
internal sealed class DesignTimePublicDbContextFactory : IDesignTimeDbContextFactory<PublicDbContext>
{
    public PublicDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables("GOLDBANK_")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        var optionsBuilder = new DbContextOptionsBuilder<PublicDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly("GoldBank.Migrator");
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Public", "bank");
        });

        return new PublicDbContext(optionsBuilder.Options);
    }
}
