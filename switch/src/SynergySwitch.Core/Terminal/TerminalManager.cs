using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SynergySwitch.Core.Interfaces;
using SynergySwitch.Core.Models;
using SynergySwitch.Data;
using SynergySwitch.Data.Entities;

namespace SynergySwitch.Core.Terminal;

/// <summary>
/// Manages terminal registration, heartbeats, and configuration distribution.
/// </summary>
public class TerminalManager : ITerminalManager
{
    private readonly SwitchDbContext _db;
    private readonly ILogger<TerminalManager> _logger;

    public TerminalManager(SwitchDbContext db, ILogger<TerminalManager> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TerminalRegistrationResult> RegisterTerminalAsync(TerminalRegistration registration)
    {
        var existing = await _db.Terminals
            .FirstOrDefaultAsync(t => t.TerminalId == registration.TerminalId);

        if (existing != null)
        {
            existing.MerchantId = registration.MerchantId;
            existing.SerialNumber = registration.SerialNumber;
            existing.FirmwareVersion = registration.FirmwareVersion;
            existing.AppVersion = registration.AppVersion;
            existing.LastHeartbeat = DateTime.UtcNow;
            existing.IsActive = true;
            _logger.LogInformation("Terminal re-registered: {TerminalId}", registration.TerminalId);
        }
        else
        {
            var terminal = new TerminalEntity
            {
                TerminalId = registration.TerminalId,
                MerchantId = registration.MerchantId,
                SerialNumber = registration.SerialNumber,
                FirmwareVersion = registration.FirmwareVersion,
                AppVersion = registration.AppVersion,
                LastHeartbeat = DateTime.UtcNow,
                IsActive = true,
                RegisteredAt = DateTime.UtcNow
            };
            _db.Terminals.Add(terminal);
            _logger.LogInformation("New terminal registered: {TerminalId}", registration.TerminalId);
        }

        var merchant = await _db.Merchants
            .FirstOrDefaultAsync(m => m.MerchantId == registration.MerchantId);

        if (merchant == null)
        {
            merchant = new MerchantEntity
            {
                MerchantId = registration.MerchantId,
                Name = $"Merchant {registration.MerchantId}",
                CategoryCode = "5999",
                CountryCode = "716",
                CurrencyCode = "USD"
            };
            _db.Merchants.Add(merchant);
        }

        await _db.SaveChangesAsync();

        return new TerminalRegistrationResult
        {
            Success = true,
            Message = "Terminal registered successfully",
            Configuration = new TerminalConfig
            {
                MerchantName = merchant.Name,
                MerchantCategoryCode = merchant.CategoryCode,
                CountryCode = merchant.CountryCode,
                CurrencyCode = merchant.CurrencyCode,
                ContactlessFloorLimit = 5000,
                CvmRequiredLimit = 50000
            }
        };
    }

    public async Task RecordHeartbeatAsync(string terminalId, int batteryLevel, long transactionCount)
    {
        var terminal = await _db.Terminals
            .FirstOrDefaultAsync(t => t.TerminalId == terminalId);

        if (terminal != null)
        {
            terminal.LastHeartbeat = DateTime.UtcNow;
            terminal.BatteryLevel = batteryLevel;
            terminal.TransactionCount = transactionCount;
            await _db.SaveChangesAsync();
        }
        else
        {
            _logger.LogWarning("Heartbeat from unregistered terminal: {TerminalId}", terminalId);
        }
    }
}
