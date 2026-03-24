using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Payments.Application.Commands;
using UniBank.Core.Modules.Payments.Application.Handlers;
using UniBank.Protos.Payments;
using UniBank.SharedKernel.MultiTenancy;

namespace UniBank.Core.Modules.Payments.Grpc;

/// <summary>
/// gRPC service implementation for all payment operations (STORY-022 through STORY-028).
/// Maps proto-defined RPCs to application-layer handlers with proper error handling.
/// </summary>
public sealed class PaymentGrpcService : PaymentService.PaymentServiceBase
{
    private readonly TokenizeCardHandler _tokenizeCardHandler;
    private readonly NfcPaymentHandler _nfcPaymentHandler;
    private readonly ConfirmPaymentHandler _confirmPaymentHandler;
    private readonly GenerateQrHandler _generateQrHandler;
    private readonly QrPaymentHandler _qrPaymentHandler;
    private readonly PaymentNotificationHandler _notificationHandler;
    private readonly UniBankDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<PaymentGrpcService> _logger;

    public PaymentGrpcService(
        TokenizeCardHandler tokenizeCardHandler,
        NfcPaymentHandler nfcPaymentHandler,
        ConfirmPaymentHandler confirmPaymentHandler,
        GenerateQrHandler generateQrHandler,
        QrPaymentHandler qrPaymentHandler,
        PaymentNotificationHandler notificationHandler,
        UniBankDbContext dbContext,
        ITenantProvider tenantProvider,
        ILogger<PaymentGrpcService> logger)
    {
        _tokenizeCardHandler = tokenizeCardHandler;
        _nfcPaymentHandler = nfcPaymentHandler;
        _confirmPaymentHandler = confirmPaymentHandler;
        _generateQrHandler = generateQrHandler;
        _qrPaymentHandler = qrPaymentHandler;
        _notificationHandler = notificationHandler;
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    // ── STORY-022: NFC Payment Tokenization ──────────────────────────

    public override async Task<TokenizeCardResponse> TokenizeCard(
        TokenizeCardRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();

        if (string.IsNullOrWhiteSpace(request.AccountId) || !Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        // card_pan is optional — handler falls back to account's virtual card PAN

        if (string.IsNullOrWhiteSpace(request.DeviceId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "device_id is required."));

        var command = new TokenizeCardCommand(
            AccountId: accountId,
            CardPan: request.CardPan,
            DeviceId: request.DeviceId,
            TenantId: tenantId);

        var result = await _tokenizeCardHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(MapErrorToStatusCode(result.Error.Code), result.Error.Message));

        return new TokenizeCardResponse
        {
            Success = true,
            Token = result.Value.Token,
            TokenReference = result.Value.TokenReference,
            Message = $"Card ending in {result.Value.CardPanLast4} tokenized successfully."
        };
    }

    // ── STORY-023: NFC Contactless Payment at POS ────────────────────

    public override async Task<PaymentResponse> InitiateNFCPayment(
        NFCPaymentRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();

        if (string.IsNullOrWhiteSpace(request.AccountId) || !Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        if (string.IsNullOrWhiteSpace(request.MerchantId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "merchant_id is required."));

        if (request.Amount is null || !decimal.TryParse(request.Amount.Amount, out var amount) || amount <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid positive amount is required."));

        var currency = request.Amount.Currency;
        if (string.IsNullOrWhiteSpace(currency))
            currency = "ZWG";

        var command = new NfcPaymentCommand(
            AccountId: accountId,
            MerchantId: request.MerchantId,
            TerminalId: request.TerminalId,
            Amount: amount,
            Currency: currency,
            NfcData: request.NfcData,
            Pin: string.IsNullOrEmpty(request.Pin) ? null : request.Pin,
            TenantId: tenantId);

        var result = await _nfcPaymentHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(MapErrorToStatusCode(result.Error.Code), result.Error.Message));

        return MapToPaymentResponse(result.Value, currency);
    }

    // ── STORY-024: Confirm High-Value NFC Payment with PIN ───────────

    public override async Task<PaymentResponse> ConfirmNFCPayment(
        ConfirmNFCPaymentRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.TransactionId) || !Guid.TryParse(request.TransactionId, out var transactionId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid transaction_id is required."));

        if (string.IsNullOrWhiteSpace(request.Pin))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "pin is required."));

        // Resolve payer account from the pending payment record
        var payment = await _dbContext.Payments
            .FirstOrDefaultAsync(p => p.Id == transactionId, context.CancellationToken);

        if (payment is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Payment not found."));

        var command = new ConfirmPaymentCommand(
            TransactionId: transactionId,
            Pin: request.Pin,
            AccountId: payment.PayerAccountId);

        var result = await _confirmPaymentHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(MapErrorToStatusCode(result.Error.Code), result.Error.Message));

        return MapToPaymentResponse(result.Value, result.Value.Currency);
    }

    // ── STORY-025, STORY-028: Payment Notification Streaming ─────────

    public override async Task StreamPaymentNotifications(
        StreamNotificationsRequest request,
        IServerStreamWriter<PaymentNotification> responseStream,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.AccountId) || !Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        _logger.LogInformation("Starting payment notification stream for account {AccountId}", accountId);

        var reader = _notificationHandler.Subscribe(accountId);

        try
        {
            await foreach (var notification in reader.ReadAllAsync(context.CancellationToken))
            {
                var proto = new PaymentNotification
                {
                    NotificationId = notification.NotificationId,
                    TransactionId = notification.TransactionId,
                    Type = notification.Type,
                    Title = notification.Title,
                    Body = notification.Body,
                    Amount = new UniBank.Protos.Common.Money
                    {
                        Amount = notification.Amount.ToString("F2"),
                        Currency = notification.Currency
                    },
                    Status = notification.Status,
                    Reference = notification.Reference,
                    CreatedAt = Timestamp.FromDateTime(
                        DateTime.SpecifyKind(notification.CreatedAt, DateTimeKind.Utc))
                };

                await responseStream.WriteAsync(proto, context.CancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Payment notification stream cancelled for account {AccountId}", accountId);
        }
        finally
        {
            _notificationHandler.Unsubscribe(accountId);
        }
    }

    // ── STORY-026: Generate EMV QR Code ──────────────────────────────

    public override async Task<QRCodeResponse> GenerateQRCode(
        QRCodeRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();

        if (string.IsNullOrWhiteSpace(request.MerchantId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "merchant_id is required."));

        if (request.Amount is null || !decimal.TryParse(request.Amount.Amount, out var amount) || amount <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid positive amount is required."));

        var currency = request.Amount.Currency;
        if (string.IsNullOrWhiteSpace(currency))
            currency = "ZWG";

        var ttlSeconds = request.TtlSeconds > 0 ? request.TtlSeconds : 300;

        var command = new GenerateQrCommand(
            MerchantId: request.MerchantId,
            TerminalId: string.IsNullOrEmpty(request.TerminalId) ? null : request.TerminalId,
            Amount: amount,
            Currency: currency,
            Description: request.Description,
            TtlSeconds: ttlSeconds,
            TenantId: tenantId);

        var result = await _generateQrHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(MapErrorToStatusCode(result.Error.Code), result.Error.Message));

        return new QRCodeResponse
        {
            Success = true,
            QrCodeData = result.Value.QrCodeData,
            PaymentReference = result.Value.PaymentReference,
            ExpiresAt = Timestamp.FromDateTime(
                DateTime.SpecifyKind(result.Value.ExpiresAt, DateTimeKind.Utc))
        };
    }

    // ── STORY-027: Scan QR Code & Process Payment ────────────────────

    public override async Task<PaymentResponse> ProcessQRPayment(
        QRPaymentRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();

        if (string.IsNullOrWhiteSpace(request.AccountId) || !Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        if (string.IsNullOrWhiteSpace(request.QrCodeData))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "qr_code_data is required."));

        if (string.IsNullOrWhiteSpace(request.Pin))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "pin is required."));

        var command = new QrPaymentCommand(
            AccountId: accountId,
            QrCodeData: request.QrCodeData,
            Pin: request.Pin,
            TenantId: tenantId);

        var result = await _qrPaymentHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(MapErrorToStatusCode(result.Error.Code), result.Error.Message));

        return MapToPaymentResponse(result.Value, result.Value.Currency);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static PaymentResponse MapToPaymentResponse(PaymentResult value, string currency)
    {
        var response = new PaymentResponse
        {
            Success = true,
            Message = value.RequiresPin ? "PIN required to complete payment." : "Payment processed successfully.",
            TransactionId = value.TransactionId,
            Reference = value.Reference,
            Amount = new UniBank.Protos.Common.Money
            {
                Amount = value.Amount.ToString("F2"),
                Currency = currency
            },
            Fee = new UniBank.Protos.Common.Money
            {
                Amount = value.Fee.ToString("F2"),
                Currency = currency
            },
            NewBalance = new UniBank.Protos.Common.Money
            {
                Amount = value.NewBalance.ToString("F2"),
                Currency = currency
            },
            Tax = new UniBank.Protos.Common.Money
            {
                Amount = value.Tax.ToString("F2"),
                Currency = currency
            },
            MerchantCommission = new UniBank.Protos.Common.Money
            {
                Amount = value.MerchantCommission.ToString("F2"),
                Currency = currency
            },
            RequiresPin = value.RequiresPin,
            Status = value.Status
        };

        if (value.CompletedAt.HasValue)
        {
            response.CompletedAt = Timestamp.FromDateTime(
                DateTime.SpecifyKind(value.CompletedAt.Value, DateTimeKind.Utc));
        }

        return response;
    }

    private static StatusCode MapErrorToStatusCode(string errorCode)
    {
        return errorCode switch
        {
            "Account.NotFound" => StatusCode.NotFound,
            "Account.Inactive" => StatusCode.FailedPrecondition,
            "Account.NoPinSet" => StatusCode.FailedPrecondition,
            "Merchant.NotFound" => StatusCode.NotFound,
            "Merchant.AccountNotFound" => StatusCode.NotFound,
            "Payment.NotFound" => StatusCode.NotFound,
            "Payment.Unauthorized" => StatusCode.PermissionDenied,
            "Payment.Expired" => StatusCode.DeadlineExceeded,
            "Payment.InsufficientFunds" => StatusCode.FailedPrecondition,
            "Payment.InvalidAmount" => StatusCode.InvalidArgument,
            "Payment.SelfPayment" => StatusCode.InvalidArgument,
            "Payment.PinRequired" => StatusCode.InvalidArgument,
            "Token.InvalidPan" => StatusCode.InvalidArgument,
            "QR.InvalidAmount" => StatusCode.InvalidArgument,
            "QR.InvalidData" => StatusCode.InvalidArgument,
            "QR.InvalidFormat" => StatusCode.InvalidArgument,
            "QR.Expired" => StatusCode.DeadlineExceeded,
            "QR.CorruptData" => StatusCode.Internal,
            "Auth.InvalidPIN" => StatusCode.Unauthenticated,
            "Auth.Locked" => StatusCode.PermissionDenied,
            _ => StatusCode.Internal,
        };
    }
}
