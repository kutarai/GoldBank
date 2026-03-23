using SynergySwitch.Core.Models;

namespace SynergySwitch.Core.Interfaces;

/// <summary>
/// Manages terminal lifecycle: registration, heartbeats, config pushes.
/// </summary>
public interface ITerminalManager
{
    Task<TerminalRegistrationResult> RegisterTerminalAsync(TerminalRegistration registration);

    Task RecordHeartbeatAsync(
        string terminalId,
        int batteryLevel,
        long transactionCount);
}
