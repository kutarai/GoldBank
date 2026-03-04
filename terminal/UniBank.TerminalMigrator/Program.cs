using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UniBank.TerminalManager.Infrastructure;

namespace UniBank.TerminalMigrator;

/// <summary>
/// Standalone migration runner for the TerminalDbContext.
///
/// Usage:
///   dotnet run
///
/// Design-time (dotnet ef):
///   dotnet ef migrations add Initial --context TerminalDbContext
/// </summary>
internal sealed class Program
{
    private static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables("UNIBANK_")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        Console.WriteLine("Applying TerminalDbContext migrations...");

        var options = new DbContextOptionsBuilder<TerminalDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new TerminalDbContext(options);
        await db.Database.MigrateAsync();

        Console.WriteLine("TerminalDbContext migrations applied successfully.");
    }
}
