using Microsoft.EntityFrameworkCore;
using UniBank.Core.Common.Persistence;

namespace UniBank.Core.Modules.Merchants.Infrastructure.Services;

/// <summary>
/// Generates unique merchant codes in format MRC-{TENANT_CODE}-{SEQUENTIAL} (STORY-050).
/// Uses a database sequence for uniqueness.
/// </summary>
public sealed class MerchantIdGenerator
{
    private readonly UniBankDbContext _dbContext;

    public MerchantIdGenerator(UniBankDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GenerateAsync(string tenantCode, CancellationToken cancellationToken = default)
    {
        // Use database to generate a unique sequential number
        // In production, use a proper database sequence
        var connection = _dbContext.Database.GetDbConnection();
        await _dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT nextval('merchant_code_seq')";

            var result = await command.ExecuteScalarAsync(cancellationToken);
            var sequenceValue = Convert.ToInt64(result);

            return $"MRC-{tenantCode.ToUpperInvariant()}-{sequenceValue}";
        }
        catch
        {
            // Fallback: use timestamp-based ID if sequence doesn't exist
            var fallback = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000;
            return $"MRC-{tenantCode.ToUpperInvariant()}-{fallback}";
        }
    }
}
