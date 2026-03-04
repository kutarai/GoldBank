using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using UniBank.TerminalManager.Infrastructure;

namespace UniBank.TerminalMigrator.DesignTime;

/// <summary>
/// Factory used by <c>dotnet ef</c> tooling to create a design-time <see cref="TerminalDbContext"/>.
///
/// Usage:
///   dotnet ef migrations add Initial --context TerminalDbContext
///   dotnet ef database update --context TerminalDbContext
/// </summary>
internal sealed class DesignTimeTerminalDbContextFactory : IDesignTimeDbContextFactory<TerminalDbContext>
{
    public TerminalDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables("UNIBANK_")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        var optionsBuilder = new DbContextOptionsBuilder<TerminalDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly("UniBank.TerminalMigrator");
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "terminal_mgmt");
        });

        return new TerminalDbContext(optionsBuilder.Options);
    }
}
