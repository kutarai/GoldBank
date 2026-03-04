using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UniBank.Protos.HSM;
using UniBank.SharedKernel.Results;
using UniBank.TerminalManager.Domain;
using UniBank.TerminalManager.Infrastructure;

namespace UniBank.TerminalManager.Services;

/// <summary>
/// Terminal encryption key lifecycle management via HSM gRPC (STORY-047).
/// Generates master keys, derives session keys, and tracks key rotation schedules.
/// </summary>
public sealed class TerminalKeyManager
{
    private readonly TerminalDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TerminalKeyManager> _logger;

    public TerminalKeyManager(
        TerminalDbContext dbContext,
        IConfiguration configuration,
        ILogger<TerminalKeyManager> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Generates a master key for a terminal via HSM and stores key metadata.
    /// </summary>
    public async Task<Result<TerminalKeyInfo>> GenerateMasterKeyAsync(
        Guid terminalId,
        CancellationToken cancellationToken = default)
    {
        var terminal = await _dbContext.Terminals
            .FirstOrDefaultAsync(t => t.Id == terminalId, cancellationToken);

        if (terminal is null)
        {
            return Result.Failure<TerminalKeyInfo>(
                new Error("Terminal.NotFound", $"Terminal '{terminalId}' not found."));
        }

        var hsmEndpoint = _configuration.GetValue<string>("Hsm:GrpcEndpoint") ?? "http://localhost:5010";

        using var channel = GrpcChannel.ForAddress(hsmEndpoint);
        var client = new HSMService.HSMServiceClient(channel);

        var generateResponse = await client.GenerateKeyAsync(new GenerateKeyRequest
        {
            KeyType = KeyType.Master,
            KeyLength = 256,
            KeyLabel = $"terminal-master-{terminal.SerialNumber}"
        }, cancellationToken: cancellationToken);

        if (!generateResponse.Success)
        {
            return Result.Failure<TerminalKeyInfo>(
                new Error("Terminal.KeyGenFailed", "Failed to generate master key via HSM."));
        }

        // Check for existing key info and update, or create new
        var keyInfo = await _dbContext.TerminalKeyInfos
            .FirstOrDefaultAsync(k => k.TerminalId == terminalId, cancellationToken);

        if (keyInfo is null)
        {
            keyInfo = new TerminalKeyInfo
            {
                TerminalId = terminalId,
                MasterKeyId = generateResponse.KeyId,
                LastRotation = DateTime.UtcNow,
                NextRotation = DateTime.UtcNow.AddDays(90)
            };
            _dbContext.TerminalKeyInfos.Add(keyInfo);
        }
        else
        {
            keyInfo.MasterKeyId = generateResponse.KeyId;
            keyInfo.LastRotation = DateTime.UtcNow;
            keyInfo.NextRotation = DateTime.UtcNow.AddDays(90);
        }

        terminal.LastKeyInjection = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Master key generated: TerminalId={TerminalId}, KeyId={KeyId}, NextRotation={NextRotation}",
            terminalId, generateResponse.KeyId, keyInfo.NextRotation);

        return Result.Success(keyInfo);
    }

    /// <summary>
    /// Derives a session key from the terminal's master key via HSM.
    /// </summary>
    public async Task<Result<string>> DeriveSessionKeyAsync(
        Guid terminalId,
        string derivationData,
        CancellationToken cancellationToken = default)
    {
        var keyInfo = await _dbContext.TerminalKeyInfos
            .FirstOrDefaultAsync(k => k.TerminalId == terminalId, cancellationToken);

        if (keyInfo is null)
        {
            return Result.Failure<string>(
                new Error("Terminal.NoMasterKey", "No master key found for terminal. Generate a master key first."));
        }

        var hsmEndpoint = _configuration.GetValue<string>("Hsm:GrpcEndpoint") ?? "http://localhost:5010";

        using var channel = GrpcChannel.ForAddress(hsmEndpoint);
        var client = new HSMService.HSMServiceClient(channel);

        var deriveResponse = await client.DeriveSessionKeyAsync(new DeriveSessionKeyRequest
        {
            MasterKeyId = keyInfo.MasterKeyId,
            DerivationData = derivationData,
            DerivedKeyType = KeyType.Session
        }, cancellationToken: cancellationToken);

        if (!deriveResponse.Success)
        {
            return Result.Failure<string>(
                new Error("Terminal.SessionKeyFailed", "Failed to derive session key via HSM."));
        }

        keyInfo.ActiveSessionKeyId = deriveResponse.SessionKeyId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Session key derived: TerminalId={TerminalId}, SessionKeyId={SessionKeyId}",
            terminalId, deriveResponse.SessionKeyId);

        return Result.Success(deriveResponse.SessionKeyId);
    }

    /// <summary>
    /// Handles key injection response from a terminal on the MQTT keys topic.
    /// </summary>
    public async Task HandleKeyInjectionResponseAsync(
        string tenantId,
        string terminalId,
        string payload,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Key injection response received: TenantId={TenantId}, TerminalId={TerminalId}",
            tenantId, terminalId);

        // Find the terminal by serial number (terminalId in MQTT context is the serial)
        var terminal = await _dbContext.Terminals
            .FirstOrDefaultAsync(
                t => t.SerialNumber == terminalId && t.TenantId == tenantId,
                cancellationToken);

        if (terminal is not null)
        {
            terminal.LastKeyInjection = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
