using Grpc.Core;
using SynergySwitch.Core.Interfaces;
using SynergySwitch.Core.Models;
using SynergySwitch.Protos.Terminal;

namespace SynergySwitch.Api.Services;

/// <summary>
/// gRPC service for terminal lifecycle management.
/// Maps between protobuf types and domain models.
/// </summary>
public class TerminalManagementGrpcService : TerminalManagementService.TerminalManagementServiceBase
{
    private readonly ITerminalManager _terminalManager;
    private readonly ILogger<TerminalManagementGrpcService> _logger;

    public TerminalManagementGrpcService(
        ITerminalManager terminalManager,
        ILogger<TerminalManagementGrpcService> logger)
    {
        _terminalManager = terminalManager;
        _logger = logger;
    }

    public override async Task<TerminalRegistrationResponse> Register(
        TerminalRegistrationRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation(
            "Terminal registration: terminalId={TerminalId}, merchantId={MerchantId}",
            request.TerminalId, request.MerchantId);

        var registration = new TerminalRegistration
        {
            TerminalId = request.TerminalId,
            MerchantId = request.MerchantId,
            SerialNumber = request.SerialNumber,
            FirmwareVersion = request.FirmwareVersion,
            AppVersion = request.AppVersion
        };

        var result = await _terminalManager.RegisterTerminalAsync(registration);

        var response = new TerminalRegistrationResponse
        {
            Success = result.Success,
            Message = result.Message ?? ""
        };

        if (result.Configuration != null)
        {
            response.Configuration = new TerminalConfiguration
            {
                MerchantName = result.Configuration.MerchantName ?? "",
                MerchantCategoryCode = result.Configuration.MerchantCategoryCode ?? "",
                CountryCode = result.Configuration.CountryCode ?? "",
                CurrencyCode = result.Configuration.CurrencyCode ?? "",
                ContactlessFloorLimit = result.Configuration.ContactlessFloorLimit,
                CvmRequiredLimit = result.Configuration.CvmRequiredLimit
            };
        }

        return response;
    }

    public override async Task<HeartbeatResponse> Heartbeat(
        HeartbeatRequest request,
        ServerCallContext context)
    {
        await _terminalManager.RecordHeartbeatAsync(
            request.TerminalId,
            request.BatteryLevel,
            request.TransactionCount);

        return new HeartbeatResponse
        {
            Acknowledged = true,
            ServerTime = DateTime.UtcNow.ToString("o")
        };
    }

    public override async Task StreamUpdates(
        TerminalIdentifier request,
        IServerStreamWriter<TerminalUpdate> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation("StreamUpdates started for terminal {TerminalId}", request.TerminalId);

        try
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), context.CancellationToken);
                // Future: push config/key updates when available
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("StreamUpdates ended for terminal {TerminalId}", request.TerminalId);
        }
    }
}
