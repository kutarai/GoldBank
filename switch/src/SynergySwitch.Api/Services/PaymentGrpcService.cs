using Grpc.Core;
using SynergySwitch.Core.Interfaces;
using SynergySwitch.Core.Models;
using SynergySwitch.Protos.Payment;

namespace SynergySwitch.Api.Services;

/// <summary>
/// gRPC service implementing ISO 20022 AcceptorAuthorisation (caaa.001/caaa.002).
/// Maps between protobuf types and domain models.
/// </summary>
public class PaymentGrpcService : PaymentService.PaymentServiceBase
{
    private readonly IAuthorisationProcessor _processor;
    private readonly IQrPaymentManager _qrPaymentManager;
    private readonly IMobileMoneyPaymentManager _mobileMoneyPaymentManager;
    private readonly ILogger<PaymentGrpcService> _logger;
    private const int QrPollIntervalMs = 1000;
    private const int MobileMoneyPollIntervalMs = 1000;

    public PaymentGrpcService(
        IAuthorisationProcessor processor,
        IQrPaymentManager qrPaymentManager,
        IMobileMoneyPaymentManager mobileMoneyPaymentManager,
        ILogger<PaymentGrpcService> logger)
    {
        _processor = processor;
        _qrPaymentManager = qrPaymentManager;
        _mobileMoneyPaymentManager = mobileMoneyPaymentManager;
        _logger = logger;
    }

    public override async Task<AcceptorAuthorisationResponse> Authorise(
        AcceptorAuthorisationRequest request,
        ServerCallContext context)
    {
        var exchangeId = request.Header?.ExchangeId ?? Guid.NewGuid().ToString();

        try
        {
            // Map protobuf → domain
            var domainRequest = MapToDomain(request);

            // Process
            var domainResponse = await _processor.ProcessAuthorisationAsync(domainRequest);

            // Map domain → protobuf
            return MapToProto(domainResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authorise failed: exchangeId={ExchangeId}", exchangeId);

            return new AcceptorAuthorisationResponse
            {
                Header = new MessageHeader
                {
                    MessageFunction = "AUTP",
                    ProtocolVersion = "14.0",
                    ExchangeId = exchangeId,
                    CreationDateTime = DateTime.UtcNow.ToString("o")
                },
                TransactionReference = request.Transaction?.TransactionReference ?? "",
                Result = new AuthorisationResult
                {
                    Response = ResponseCode.Tech,
                    ResponseReason = "9999",
                    EmvResponseCode = "3936"
                },
                DisplayMessage = "System error — please try again"
            };
        }
    }

    private static AuthorisationRequest MapToDomain(AcceptorAuthorisationRequest proto)
    {
        return new AuthorisationRequest
        {
            ExchangeId = proto.Header?.ExchangeId ?? Guid.NewGuid().ToString(),
            TerminalId = proto.Header?.InitiatingPartyId ?? "",
            MerchantId = proto.Environment?.Merchant?.Id ?? "",
            MerchantName = proto.Environment?.Merchant?.CommonName,
            MerchantCategoryCode = proto.Environment?.Merchant?.CategoryCode,
            Pan = proto.Environment?.Card?.Pan ?? "",
            CardSequenceNumber = proto.Environment?.Card?.CardSequenceNumber,
            ExpiryDate = proto.Environment?.Card?.ExpiryDate,
            Track2EquivalentData = proto.Environment?.Card?.Track2EquivalentData,
            CvmMethod = proto.Environment?.CardholderAuth?.Method.ToString() ?? "NO_CVM",
            EncryptedPinBlock = proto.Environment?.CardholderAuth?.EncryptedPinBlock?.ToByteArray(),
            TransactionReference = proto.Transaction?.TransactionReference ?? "",
            Currency = proto.Transaction?.Currency ?? "USD",
            Amount = proto.Transaction?.Amount ?? 0,
            CardEntryMode = proto.Context?.CardDataEntryMode.ToString() ?? "CICC",
            IccRelatedData = proto.Transaction?.IccRelatedData?.ToByteArray()
        };
    }

    private static AcceptorAuthorisationResponse MapToProto(AuthorisationResponse domain)
    {
        return new AcceptorAuthorisationResponse
        {
            Header = new MessageHeader
            {
                MessageFunction = "AUTP",
                ProtocolVersion = "14.0",
                ExchangeId = domain.ExchangeId,
                CreationDateTime = DateTime.UtcNow.ToString("o"),
                InitiatingPartyId = "SYNERGY_SWITCH"
            },
            TransactionReference = domain.TransactionReference,
            Result = new AuthorisationResult
            {
                Response = domain.ResponseCode switch
                {
                    AuthorisationResponseCode.Approved => ResponseCode.Appr,
                    AuthorisationResponseCode.Declined => ResponseCode.Decl,
                    AuthorisationResponseCode.Partial => ResponseCode.Prms,
                    _ => ResponseCode.Tech
                },
                ResponseReason = domain.ResponseReason,
                AuthorisationCode = domain.AuthorisationCode ?? "",
                EmvResponseCode = domain.EmvResponseCode
            },
            DisplayMessage = domain.DisplayMessage ?? ""
        };
    }

    // ─── QR Payment Streaming ────────────────────────────────────────

    /// <summary>
    /// Server-streaming RPC for EMVco QR payments.
    /// 1. Registers payment as PENDING
    /// 2. Sends PENDING confirmation to terminal
    /// 3. Polls DB for status change (CLAIMED by bank notification)
    /// 4. If terminal closes stream → marks payment TIMED_OUT
    /// </summary>
    public override async Task WaitForQrPayment(
        QrPaymentRequest request,
        IServerStreamWriter<QrPaymentUpdate> responseStream,
        ServerCallContext context)
    {
        var paymentRef = request.PaymentReference;
        _logger.LogInformation(
            "WaitForQrPayment: ref={Ref}, terminal={Terminal}, amount={Amount} {Currency}, gps=({Lat},{Lng})",
            paymentRef, request.TerminalId, request.Amount, request.Currency,
            request.Latitude, request.Longitude);

        try
        {
            // 1. Register the payment as PENDING (with GPS if provided)
            var lat = request.Latitude != 0 ? (double?)request.Latitude : null;
            var lng = request.Longitude != 0 ? (double?)request.Longitude : null;
            await _qrPaymentManager.RegisterPaymentAsync(
                paymentRef, request.TerminalId, request.MerchantId,
                request.Currency, request.Amount, request.QrPayload,
                lat, lng);

            // 2. Send PENDING confirmation to terminal
            await responseStream.WriteAsync(new QrPaymentUpdate
            {
                Status = QrPaymentStatus.QrPending,
                Message = "Payment registered, waiting for customer",
                Timestamp = DateTime.UtcNow.ToString("o")
            });

            // 3. Poll for status change until cancelled or claimed
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(QrPollIntervalMs, context.CancellationToken);

                var payment = await _qrPaymentManager.GetPaymentAsync(paymentRef);
                if (payment is null) break;

                switch (payment.Status)
                {
                    case "CLAIMED":
                        await responseStream.WriteAsync(new QrPaymentUpdate
                        {
                            Status = QrPaymentStatus.QrClaimed,
                            AuthorizationCode = payment.AuthorizationCode ?? "",
                            ProviderReference = payment.ProviderReference ?? "",
                            Message = "Payment confirmed",
                            Timestamp = DateTime.UtcNow.ToString("o")
                        });
                        _logger.LogInformation("QR payment claimed: ref={Ref}", paymentRef);
                        return;

                    case "DECLINED":
                        await responseStream.WriteAsync(new QrPaymentUpdate
                        {
                            Status = QrPaymentStatus.QrDeclined,
                            Message = "Payment declined",
                            Timestamp = DateTime.UtcNow.ToString("o")
                        });
                        return;

                    case "TIMED_OUT":
                        await responseStream.WriteAsync(new QrPaymentUpdate
                        {
                            Status = QrPaymentStatus.QrTimedOut,
                            Message = "Payment timed out",
                            Timestamp = DateTime.UtcNow.ToString("o")
                        });
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Terminal closed QR payment stream: ref={Ref}", paymentRef);
        }
        catch (Exception ex) when (ex is RpcException rpc && rpc.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogInformation("QR payment stream cancelled: ref={Ref}", paymentRef);
        }
        finally
        {
            // Mark as TIMED_OUT if still PENDING (terminal disconnected)
            var payment = await _qrPaymentManager.GetPaymentAsync(paymentRef);
            if (payment is { Status: "PENDING" })
            {
                await _qrPaymentManager.MarkTimedOutAsync(paymentRef);
                _logger.LogInformation("Marked TIMED_OUT after stream close: ref={Ref}", paymentRef);
            }
        }
    }

    // ─── Mobile Money Payment Streaming ──────────────────────────────

    /// <summary>
    /// Server-streaming RPC for push-initiated mobile money payments.
    /// 1. Registers payment as PENDING and sends request to provider (via DB polling pattern)
    /// 2. Sends PENDING confirmation to terminal
    /// 3. Polls DB for status change (CONFIRMED or DECLINED set by provider callback)
    /// 4. If terminal closes stream (20s timeout) → marks payment TIMED_OUT
    /// </summary>
    public override async Task InitiateMobileMoneyPayment(
        MobileMoneyPaymentRequest request,
        IServerStreamWriter<MobileMoneyPaymentUpdate> responseStream,
        ServerCallContext context)
    {
        var paymentRef = request.PaymentReference;
        _logger.LogInformation(
            "InitiateMobileMoneyPayment: ref={Ref}, terminal={Terminal}, mobile={Mobile}, amount={Amount} {Currency}",
            paymentRef, request.TerminalId, request.MobileNumber, request.Amount, request.Currency);

        try
        {
            // 1. Register the payment as PENDING
            var lat = request.Latitude != 0 ? (double?)request.Latitude : null;
            var lng = request.Longitude != 0 ? (double?)request.Longitude : null;
            await _mobileMoneyPaymentManager.RegisterPaymentAsync(
                paymentRef, request.TerminalId, request.MerchantId,
                request.Currency, request.Amount, request.MobileNumber,
                lat, lng);

            // 2. Send PENDING confirmation — terminal starts countdown
            await responseStream.WriteAsync(new MobileMoneyPaymentUpdate
            {
                Status = MobileMoneyPaymentStatus.MobilePending,
                Message = "Payment request sent, waiting for customer confirmation",
                Timestamp = DateTime.UtcNow.ToString("o")
            });

            // 3. Poll for status change until cancelled or terminal closes stream
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(MobileMoneyPollIntervalMs, context.CancellationToken);

                var payment = await _mobileMoneyPaymentManager.GetPaymentAsync(paymentRef);
                if (payment is null) break;

                switch (payment.Status)
                {
                    case "CONFIRMED":
                        await responseStream.WriteAsync(new MobileMoneyPaymentUpdate
                        {
                            Status = MobileMoneyPaymentStatus.MobileConfirmed,
                            AuthorizationCode = payment.AuthorizationCode ?? "",
                            ProviderReference = payment.ProviderReference ?? "",
                            Message = "Payment confirmed",
                            Timestamp = DateTime.UtcNow.ToString("o")
                        });
                        _logger.LogInformation("Mobile money payment confirmed: ref={Ref}", paymentRef);
                        return;

                    case "DECLINED":
                        await responseStream.WriteAsync(new MobileMoneyPaymentUpdate
                        {
                            Status = MobileMoneyPaymentStatus.MobileDeclined,
                            Message = "Payment declined",
                            Timestamp = DateTime.UtcNow.ToString("o")
                        });
                        _logger.LogInformation("Mobile money payment declined: ref={Ref}", paymentRef);
                        return;

                    case "TIMED_OUT":
                        await responseStream.WriteAsync(new MobileMoneyPaymentUpdate
                        {
                            Status = MobileMoneyPaymentStatus.MobileTimedOut,
                            Message = "Payment timed out",
                            Timestamp = DateTime.UtcNow.ToString("o")
                        });
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Terminal closed mobile money payment stream: ref={Ref}", paymentRef);
        }
        catch (Exception ex) when (ex is RpcException rpc && rpc.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogInformation("Mobile money payment stream cancelled: ref={Ref}", paymentRef);
        }
        finally
        {
            // Mark as TIMED_OUT if still PENDING (terminal disconnected / 20s timeout)
            var payment = await _mobileMoneyPaymentManager.GetPaymentAsync(paymentRef);
            if (payment is { Status: "PENDING" })
            {
                await _mobileMoneyPaymentManager.MarkTimedOutAsync(paymentRef);
                _logger.LogInformation("Marked mobile money TIMED_OUT after stream close: ref={Ref}", paymentRef);
            }
        }
    }
}
