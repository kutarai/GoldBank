using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.SharedKernel.Results;
using UniBank.TerminalManager.Domain;
using UniBank.TerminalManager.Infrastructure;

namespace UniBank.TerminalManager.Services;

/// <summary>
/// Handles terminal registration and provisioning (STORY-046).
/// Generates MQTT topic prefixes and logs audit trail for terminal onboarding.
/// </summary>
public sealed class TerminalRegistrationService
{
    private readonly TerminalDbContext _dbContext;
    private readonly ILogger<TerminalRegistrationService> _logger;

    public TerminalRegistrationService(
        TerminalDbContext dbContext,
        ILogger<TerminalRegistrationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<Terminal>> RegisterAsync(
        Guid merchantId,
        string tenantId,
        string serialNumber,
        string model,
        string firmwareVersion,
        string? location,
        CancellationToken cancellationToken = default)
    {
        // Check for duplicate serial number
        var existing = await _dbContext.Terminals
            .AnyAsync(t => t.SerialNumber == serialNumber, cancellationToken);

        if (existing)
        {
            return Result.Failure<Terminal>(
                new Error("Terminal.DuplicateSerial", $"Terminal with serial number '{serialNumber}' already exists."));
        }

        var terminal = new Terminal
        {
            MerchantId = merchantId,
            TenantId = tenantId,
            SerialNumber = serialNumber,
            Model = model,
            FirmwareVersion = firmwareVersion,
            Location = location,
            Status = "inactive",
            MqttTopicPrefix = GenerateMqttTopicPrefix(tenantId, serialNumber),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Terminals.Add(terminal);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Terminal registered: Id={TerminalId}, SerialNumber={SerialNumber}, TenantId={TenantId}, MerchantId={MerchantId}, MqttPrefix={MqttPrefix}",
            terminal.Id, serialNumber, tenantId, merchantId, terminal.MqttTopicPrefix);

        return Result.Success(terminal);
    }

    private static string GenerateMqttTopicPrefix(string tenantId, string serialNumber)
    {
        // Topic format: terminal/{tenantId}/{terminalSerialNumber}
        // This aligns with the MqttTopicHandler routing convention
        return $"terminal/{tenantId}/{serialNumber}";
    }
}
