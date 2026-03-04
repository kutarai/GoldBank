using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Protos.Common;
using UniBank.Protos.Terminals;
using UniBank.TerminalManager.Infrastructure;
using UniBank.TerminalManager.Services;

namespace UniBank.TerminalManager.Grpc;

/// <summary>
/// gRPC service implementing the TerminalService proto (STORY-046, STORY-048, STORY-049).
/// Handles terminal registration, status queries, and remote software updates.
/// </summary>
public sealed class TerminalGrpcService : TerminalService.TerminalServiceBase
{
    private readonly TerminalRegistrationService _registrationService;
    private readonly TerminalUpdateService _updateService;
    private readonly TerminalDbContext _dbContext;
    private readonly ILogger<TerminalGrpcService> _logger;

    public TerminalGrpcService(
        TerminalRegistrationService registrationService,
        TerminalUpdateService updateService,
        TerminalDbContext dbContext,
        ILogger<TerminalGrpcService> logger)
    {
        _registrationService = registrationService;
        _updateService = updateService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new EFT POS terminal (STORY-046).
    /// </summary>
    public override async Task<RegisterTerminalResponse> RegisterTerminal(
        RegisterTerminalRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.SerialNumber))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Serial number is required."));

        if (string.IsNullOrWhiteSpace(request.MerchantId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Merchant ID is required."));

        _logger.LogDebug(
            "RegisterTerminal: SerialNumber={SerialNumber}, MerchantId={MerchantId}",
            request.SerialNumber, request.MerchantId);

        // Extract tenant from gRPC metadata or use a default
        var tenantId = context.RequestHeaders.GetValue("x-tenant-id") ?? "default";

        var result = await _registrationService.RegisterAsync(
            Guid.Parse(request.MerchantId),
            tenantId,
            request.SerialNumber,
            request.Model,
            request.FirmwareVersion,
            request.LocationDescription,
            context.CancellationToken);

        if (result.IsFailure)
        {
            var statusCode = result.Error.Code switch
            {
                "Terminal.DuplicateSerial" => StatusCode.AlreadyExists,
                _ => StatusCode.Internal,
            };
            throw new RpcException(new Status(statusCode, result.Error.Message));
        }

        var terminal = result.Value;

        return new RegisterTerminalResponse
        {
            Success = true,
            Message = "Terminal registered successfully.",
            TerminalId = terminal.Id.ToString(),
            MqttTopicPrefix = terminal.MqttTopicPrefix,
            InitialConfig = "{}"
        };
    }

    /// <summary>
    /// Returns the current status of a terminal (STORY-048).
    /// </summary>
    public override async Task<TerminalStatusResponse> GetTerminalStatus(
        TerminalStatusRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.TerminalId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Terminal ID is required."));

        var terminalId = Guid.Parse(request.TerminalId);
        var terminal = await _dbContext.Terminals
            .FirstOrDefaultAsync(t => t.Id == terminalId, context.CancellationToken);

        if (terminal is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Terminal not found."));

        var response = new TerminalStatusResponse
        {
            TerminalId = terminal.Id.ToString(),
            SerialNumber = terminal.SerialNumber,
            Model = terminal.Model,
            FirmwareVersion = terminal.FirmwareVersion,
            Status = MapTerminalStatus(terminal.Status),
            IpAddress = terminal.IpAddress ?? string.Empty
        };

        if (terminal.LastHeartbeat.HasValue)
        {
            response.LastHeartbeat = Timestamp.FromDateTime(
                DateTime.SpecifyKind(terminal.LastHeartbeat.Value, DateTimeKind.Utc));
        }

        if (terminal.LastKeyInjection.HasValue)
        {
            response.LastKeyInjection = Timestamp.FromDateTime(
                DateTime.SpecifyKind(terminal.LastKeyInjection.Value, DateTimeKind.Utc));
        }

        return response;
    }

    /// <summary>
    /// Pushes a software/firmware/config update to a terminal (STORY-049).
    /// </summary>
    public override async Task<StatusResponse> PushUpdate(
        PushUpdateRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.TerminalId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Terminal ID is required."));

        var terminalId = Guid.Parse(request.TerminalId);

        var updateTypeName = request.UpdateType switch
        {
            UpdateType.Firmware => "firmware",
            UpdateType.Config => "config",
            UpdateType.Keys => "keys",
            _ => "unknown"
        };

        var result = await _updateService.QueueUpdateAsync(
            terminalId,
            updateTypeName,
            request.Version,
            request.Payload.ToByteArray(),
            context.CancellationToken);

        if (result.IsFailure)
        {
            var statusCode = result.Error.Code switch
            {
                "Terminal.NotFound" => StatusCode.NotFound,
                _ => StatusCode.Internal,
            };
            throw new RpcException(new Status(statusCode, result.Error.Message));
        }

        return new StatusResponse
        {
            Success = true,
            Message = $"Update queued for terminal {request.TerminalId}."
        };
    }

    private static TerminalStatus MapTerminalStatus(string status) => status switch
    {
        "inactive" => TerminalStatus.Inactive,
        "active" => TerminalStatus.Active,
        "offline" => TerminalStatus.Offline,
        "decommissioned" => TerminalStatus.Decommissioned,
        _ => TerminalStatus.Unspecified,
    };
}
