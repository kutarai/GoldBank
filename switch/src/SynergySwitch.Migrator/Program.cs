using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SynergySwitch.Data;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

// Allow --connection CLI override
var connectionString = args
    .SkipWhile(a => a != "--connection")
    .Skip(1)
    .FirstOrDefault()
    ?? configuration.GetConnectionString("SwitchDb")
    ?? throw new InvalidOperationException("No connection string provided.");

Console.WriteLine("SynergySwitch Database Migrator");
Console.WriteLine(new string('-', 40));

var optionsBuilder = new DbContextOptionsBuilder<SwitchDbContext>();
optionsBuilder.UseNpgsql(connectionString,
    b => b.MigrationsAssembly("SynergySwitch.Migrator"));

await using var db = new SwitchDbContext(optionsBuilder.Options);

var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
if (pending.Count == 0)
{
    Console.WriteLine("Database is up to date — no pending migrations.");
}
else
{
    Console.WriteLine($"Applying {pending.Count} pending migration(s):");
    foreach (var m in pending)
        Console.WriteLine($"  -> {m}");

    await db.Database.MigrateAsync();
    Console.WriteLine("Migrations applied successfully.");
}

var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
Console.WriteLine($"\nApplied migrations ({applied.Count}):");
foreach (var m in applied)
    Console.WriteLine($"  - {m}");
