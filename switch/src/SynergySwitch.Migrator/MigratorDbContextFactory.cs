using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SynergySwitch.Data;

namespace SynergySwitch.Migrator;

public class MigratorDbContextFactory : IDesignTimeDbContextFactory<SwitchDbContext>
{
    public SwitchDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("SwitchDb")
            ?? throw new InvalidOperationException("Connection string 'SwitchDb' not found in appsettings.json");

        var optionsBuilder = new DbContextOptionsBuilder<SwitchDbContext>();
        optionsBuilder.UseNpgsql(connectionString,
            b => b.MigrationsAssembly("SynergySwitch.Migrator"));

        return new SwitchDbContext(optionsBuilder.Options);
    }
}
