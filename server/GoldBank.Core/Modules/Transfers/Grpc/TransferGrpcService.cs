using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Modules.Transfers.Application.Commands;
using GoldBank.Core.Modules.Transfers.Application.Handlers;
using GoldBank.Protos.Transfers;
using GoldBank.SharedKernel.MultiTenancy;

namespace GoldBank.Core.Modules.Transfers.Grpc;

/// <summary>
/// gRPC service implementation for P2P and cross-border transfer operations (STORY-029, STORY-030, STORY-031).
/// Maps proto-defined RPCs to application-layer handlers with proper error handling and response mapping.
/// </summary>
public sealed class TransferGrpcService : TransferService.TransferServiceBase
{
    private readonly P2PTransferHandler _p2pTransferHandler;
    private readonly CrossBorderTransferHandler _crossBorderTransferHandler;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<TransferGrpcService> _logger;

    public TransferGrpcService(
        P2PTransferHandler p2pTransferHandler,
        CrossBorderTransferHandler crossBorderTransferHandler,
        ITenantProvider tenantProvider,
        ILogger<TransferGrpcService> logger)
    {
        _p2pTransferHandler = p2pTransferHandler;
        _crossBorderTransferHandler = crossBorderTransferHandler;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    // -- STORY-029: P2P Domestic Transfer ---------------------------------

    public override async Task<TransferResponse> SendP2P(
        P2PTransferRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();

        if (string.IsNullOrWhiteSpace(request.SenderAccountId) ||
            !Guid.TryParse(request.SenderAccountId, out var senderAccountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid sender_account_id is required."));

        if (string.IsNullOrWhiteSpace(request.RecipientPhone))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "recipient_phone is required."));

        if (request.Amount is null ||
            !decimal.TryParse(request.Amount.Amount, out var amount) || amount <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid positive amount is required."));

        var currency = request.Amount.Currency;
        if (string.IsNullOrWhiteSpace(currency))
            currency = "ZWG";

        if (string.IsNullOrWhiteSpace(request.Pin))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "pin is required."));

        var command = new P2PTransferCommand(
            SenderAccountId: senderAccountId,
            RecipientPhone: request.RecipientPhone,
            Amount: amount,
            Currency: currency,
            Description: string.IsNullOrEmpty(request.Description) ? null : request.Description,
            Pin: request.Pin,
            TenantId: tenantId);

        _logger.LogInformation(
            "SendP2P request received: sender {SenderId}, recipient {RecipientPhone}, amount {Amount} {Currency}",
            senderAccountId, request.RecipientPhone, amount, currency);

        var result = await _p2pTransferHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(MapErrorToStatusCode(result.Error.Code), result.Error.Message));

        return MapToTransferResponse(result.Value);
    }

    // -- STORY-030: Cross-Border Transfer ---------------------------------

    public override async Task<TransferResponse> SendCrossBorder(
        CrossBorderTransferRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();

        if (string.IsNullOrWhiteSpace(request.SenderAccountId) ||
            !Guid.TryParse(request.SenderAccountId, out var senderAccountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid sender_account_id is required."));

        if (string.IsNullOrWhiteSpace(request.RecipientPhone))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "recipient_phone is required."));

        if (string.IsNullOrWhiteSpace(request.RecipientName))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "recipient_name is required."));

        if (string.IsNullOrWhiteSpace(request.RecipientCountry))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "recipient_country is required."));

        if (request.SendAmount is null ||
            !decimal.TryParse(request.SendAmount.Amount, out var sendAmount) || sendAmount <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid positive send_amount is required."));

        var sendCurrency = request.SendAmount.Currency;
        if (string.IsNullOrWhiteSpace(sendCurrency))
            sendCurrency = "ZWG";

        if (string.IsNullOrWhiteSpace(request.ReceiveCurrency))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "receive_currency is required."));

        if (string.IsNullOrWhiteSpace(request.Pin))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "pin is required."));

        var command = new CrossBorderTransferCommand(
            SenderAccountId: senderAccountId,
            RecipientPhone: request.RecipientPhone,
            RecipientName: request.RecipientName,
            RecipientCountry: request.RecipientCountry,
            SendAmount: sendAmount,
            SendCurrency: sendCurrency,
            ReceiveCurrency: request.ReceiveCurrency,
            CorridorId: string.IsNullOrEmpty(request.CorridorId) ? null : request.CorridorId,
            Pin: request.Pin,
            TenantId: tenantId);

        _logger.LogInformation(
            "SendCrossBorder request received: sender {SenderId}, recipient {RecipientPhone}, " +
            "amount {SendAmount} {SendCurrency} -> {ReceiveCurrency}, country {Country}",
            senderAccountId, request.RecipientPhone, sendAmount, sendCurrency,
            request.ReceiveCurrency, request.RecipientCountry);

        var result = await _crossBorderTransferHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(MapErrorToStatusCode(result.Error.Code), result.Error.Message));

        return MapToTransferResponse(result.Value);
    }

    // -- Helpers -----------------------------------------------------------

    private static TransferResponse MapToTransferResponse(TransferResult value)
    {
        var response = new TransferResponse
        {
            Success = true,
            Message = value.Status == "completed"
                ? "Transfer completed successfully."
                : "Transfer is being processed.",
            TransactionId = value.TransactionId,
            Reference = value.Reference,
            AmountSent = new GoldBank.Protos.Common.Money
            {
                Amount = value.AmountSent.ToString("F2"),
                Currency = value.Currency
            },
            AmountReceived = new GoldBank.Protos.Common.Money
            {
                Amount = value.AmountReceived.ToString("F2"),
                Currency = value.ReceiveCurrency
            },
            Fee = new GoldBank.Protos.Common.Money
            {
                Amount = value.Fee.ToString("F2"),
                Currency = value.Currency
            },
            NewBalance = new GoldBank.Protos.Common.Money
            {
                Amount = value.NewBalance.ToString("F2"),
                Currency = value.Currency
            },
            Status = MapToProtoStatus(value.Status)
        };

        if (value.ExchangeRate is not null)
            response.ExchangeRate = value.ExchangeRate;

        if (value.EstimatedDelivery.HasValue)
        {
            response.EstimatedDelivery = Timestamp.FromDateTime(
                DateTime.SpecifyKind(value.EstimatedDelivery.Value, DateTimeKind.Utc));
        }

        return response;
    }

    private static TransferStatus MapToProtoStatus(string status)
    {
        return status switch
        {
            "pending" => TransferStatus.Pending,
            "processing" => TransferStatus.Processing,
            "completed" => TransferStatus.Completed,
            "failed" => TransferStatus.Failed,
            _ => TransferStatus.Unspecified,
        };
    }

    private static StatusCode MapErrorToStatusCode(string errorCode)
    {
        return errorCode switch
        {
            "Account.NotFound" => StatusCode.NotFound,
            "Account.Inactive" => StatusCode.FailedPrecondition,
            "Account.NoPinSet" => StatusCode.FailedPrecondition,
            "Transfer.RecipientNotFound" => StatusCode.NotFound,
            "Transfer.RecipientInactive" => StatusCode.FailedPrecondition,
            "Transfer.SelfTransfer" => StatusCode.InvalidArgument,
            "Transfer.InsufficientFunds" => StatusCode.FailedPrecondition,
            "Transfer.InvalidAmount" => StatusCode.InvalidArgument,
            "Exchange.UnsupportedPair" => StatusCode.InvalidArgument,
            "Auth.InvalidPIN" => StatusCode.Unauthenticated,
            _ => StatusCode.Internal,
        };
    }
}
