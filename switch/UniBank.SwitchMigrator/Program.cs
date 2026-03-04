namespace UniBank.SwitchMigrator;

/// <summary>
/// Standalone migration runner scaffold for the Switch service.
/// The switch currently has no database (purely in-memory ISO 8583/20022 processing).
/// Add a DbContext and IDesignTimeDbContextFactory here when persistence is needed.
/// </summary>
internal sealed class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("UniBank.SwitchMigrator: No database migrations configured.");
        Console.WriteLine("The switch service currently uses in-memory processing only.");
    }
}
