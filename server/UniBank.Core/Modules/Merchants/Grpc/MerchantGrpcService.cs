using FluentValidation;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using UniBank.Core.Modules.Merchants.Application.Commands;
using UniBank.Core.Modules.Merchants.Application.Handlers;
using UniBank.Protos.Common;
using UniBank.Protos.Merchants;
using UniBank.SharedKernel.MultiTenancy;

namespace UniBank.Core.Modules.Merchants.Grpc;

/// <summary>
/// gRPC service for merchant operations (STORY-050, STORY-051, STORY-052, STORY-053, STORY-054).
/// Handles merchant registration, profile management, status queries,
/// settlement calculation, transaction history, and commission reporting.
/// </summary>
public sealed class MerchantGrpcService : MerchantService.MerchantServiceBase
{
    private readonly RegisterMerchantHandler _registerHandler;
    private readonly GetMerchantProfileHandler _getProfileHandler;
    private readonly UpdateMerchantProfileHandler _updateProfileHandler;
    private readonly GetSettlementHandler _getSettlementHandler;
    private readonly GetMerchantTransactionsHandler _getTransactionsHandler;
    private readonly GetMerchantCommissionHandler _getCommissionHandler;
    private readonly IValidator<RegisterMerchantCommand> _registerValidator;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<MerchantGrpcService> _logger;

    public MerchantGrpcService(
        RegisterMerchantHandler registerHandler,
        GetMerchantProfileHandler getProfileHandler,
        UpdateMerchantProfileHandler updateProfileHandler,
        GetSettlementHandler getSettlementHandler,
        GetMerchantTransactionsHandler getTransactionsHandler,
        GetMerchantCommissionHandler getCommissionHandler,
        IValidator<RegisterMerchantCommand> registerValidator,
        ITenantProvider tenantProvider,
        ILogger<MerchantGrpcService> logger)
    {
        _registerHandler = registerHandler;
        _getProfileHandler = getProfileHandler;
        _updateProfileHandler = updateProfileHandler;
        _getSettlementHandler = getSettlementHandler;
        _getTransactionsHandler = getTransactionsHandler;
        _getCommissionHandler = getCommissionHandler;
        _registerValidator = registerValidator;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public override async Task<MerchantRegisterResponse> Register(
        MerchantRegisterRequest request, ServerCallContext context)
    {
        var tenantId = !string.IsNullOrEmpty(request.TenantId)
            ? request.TenantId
            : _tenantProvider.GetTenantId();

        var command = new RegisterMerchantCommand(
            OwnerAccountId: Guid.Parse(request.AccountId),
            BusinessName: request.BusinessName,
            BusinessType: request.BusinessType,
            RegistrationNumber: string.IsNullOrEmpty(request.RegistrationNumber) ? null : request.RegistrationNumber,
            TaxId: string.IsNullOrEmpty(request.TaxId) ? null : request.TaxId,
            CategoryCode: string.IsNullOrEmpty(request.CategoryCode) ? null : request.CategoryCode,
            BusinessAddress: FormatAddress(request.Address),
            GpsLatitude: request.Location?.Latitude,
            GpsLongitude: request.Location?.Longitude,
            GpsAccuracyMeters: request.Location?.AccuracyMeters,
            IsAgent: request.IsAgent,
            AgentTermsAccepted: request.AgentTermsAccepted,
            TenantId: tenantId);

        var validation = await _registerValidator.ValidateAsync(command, context.CancellationToken);
        if (!validation.IsValid)
        {
            var errorMessage = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            throw new RpcException(new Status(StatusCode.InvalidArgument, errorMessage));
        }

        var result = await _registerHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
        {
            var statusCode = result.Error.Code switch
            {
                "Merchant.OwnerNotFound" => StatusCode.NotFound,
                "Merchant.OwnerNotActive" => StatusCode.FailedPrecondition,
                "Merchant.AlreadyExists" => StatusCode.AlreadyExists,
                "Merchant.DuplicateName" => StatusCode.AlreadyExists,
                _ => StatusCode.Internal,
            };
            throw new RpcException(new Status(statusCode, result.Error.Message));
        }

        return new MerchantRegisterResponse
        {
            Success = true,
            Message = $"Merchant registered. Your merchant ID is {result.Value.MerchantCode}.",
            MerchantId = result.Value.MerchantCode,
            Status = Protos.Merchants.MerchantStatus.Pending
        };
    }

    public override async Task<MerchantProfileResponse> GetProfile(
        MerchantProfileRequest request, ServerCallContext context)
    {
        var merchantId = Guid.Parse(request.MerchantId);
        var result = await _getProfileHandler.HandleAsync(merchantId, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.NotFound, result.Error.Message));

        var profile = result.Value;
        return new MerchantProfileResponse
        {
            MerchantId = profile.MerchantCode,
            BusinessName = profile.BusinessName,
            BusinessType = profile.BusinessType,
            CategoryCode = profile.CategoryCode ?? string.Empty,
            Status = MapStatus(profile.Status),
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(profile.CreatedAt, DateTimeKind.Utc))
        };
    }

    public override async Task<MerchantProfileResponse> UpdateProfile(
        UpdateMerchantProfileRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var command = new UpdateMerchantProfileCommand(
            MerchantId: Guid.Parse(request.MerchantId),
            BusinessName: string.IsNullOrEmpty(request.BusinessName) ? null : request.BusinessName,
            CategoryCode: string.IsNullOrEmpty(request.CategoryCode) ? null : request.CategoryCode,
            BusinessAddress: request.Address is not null ? FormatAddress(request.Address) : null,
            GpsLatitude: request.Location?.Latitude,
            GpsLongitude: request.Location?.Longitude,
            GpsAccuracyMeters: request.Location?.AccuracyMeters,
            TenantId: tenantId);

        var result = await _updateProfileHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.NotFound, result.Error.Message));

        var profile = result.Value;
        return new MerchantProfileResponse
        {
            MerchantId = profile.MerchantCode,
            BusinessName = profile.BusinessName,
            BusinessType = profile.BusinessType,
            CategoryCode = profile.CategoryCode ?? string.Empty,
            Status = MapStatus(profile.Status),
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(profile.CreatedAt, DateTimeKind.Utc))
        };
    }

    /// <summary>
    /// STORY-052: Calculate or retrieve a merchant settlement for a given period.
    /// </summary>
    public override async Task<SettlementResponse> GetSettlement(
        GetSettlementRequest request, ServerCallContext context)
    {
        var query = new GetSettlementQuery(
            MerchantId: Guid.Parse(request.MerchantId),
            PeriodStart: request.PeriodStart.ToDateTime(),
            PeriodEnd: request.PeriodEnd.ToDateTime(),
            Currency: string.IsNullOrEmpty(request.Currency) ? null : request.Currency);

        var result = await _getSettlementHandler.HandleAsync(query, context.CancellationToken);

        if (result.IsFailure)
        {
            var statusCode = result.Error.Code switch
            {
                "Merchant.NotFound" => StatusCode.NotFound,
                _ => StatusCode.Internal,
            };
            throw new RpcException(new Status(statusCode, result.Error.Message));
        }

        var settlement = result.Value;
        var response = new SettlementResponse
        {
            SettlementId = settlement.SettlementId.ToString(),
            MerchantId = settlement.MerchantId.ToString(),
            PeriodStart = Timestamp.FromDateTime(DateTime.SpecifyKind(settlement.PeriodStart, DateTimeKind.Utc)),
            PeriodEnd = Timestamp.FromDateTime(DateTime.SpecifyKind(settlement.PeriodEnd, DateTimeKind.Utc)),
            TotalTransactions = settlement.TotalTransactions,
            GrossAmount = new Money { Amount = settlement.GrossAmount.ToString("F2"), Currency = settlement.Currency },
            TotalFees = new Money { Amount = settlement.TotalFees.ToString("F2"), Currency = settlement.Currency },
            NetAmount = new Money { Amount = settlement.NetAmount.ToString("F2"), Currency = settlement.Currency },
            Status = settlement.Status,
            Reference = settlement.Reference,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(settlement.CreatedAt, DateTimeKind.Utc))
        };

        if (settlement.PaidAt.HasValue)
            response.PaidAt = Timestamp.FromDateTime(DateTime.SpecifyKind(settlement.PaidAt.Value, DateTimeKind.Utc));

        return response;
    }

    /// <summary>
    /// STORY-053: Stream merchant transaction history with date range filtering and pagination.
    /// </summary>
    public override async Task GetTransactionHistory(
        MerchantTransactionHistoryRequest request,
        IServerStreamWriter<MerchantTransactionResponse> responseStream,
        ServerCallContext context)
    {
        var query = new GetMerchantTransactionsQuery(
            MerchantId: Guid.Parse(request.MerchantId),
            DateFrom: request.DateRange?.From?.ToDateTime(),
            DateTo: request.DateRange?.To?.ToDateTime(),
            Page: request.Pagination?.Page ?? 1,
            PageSize: request.Pagination?.PageSize ?? 50,
            TypeFilter: string.IsNullOrEmpty(request.TypeFilter) ? null : request.TypeFilter);

        var result = await _getTransactionsHandler.HandleAsync(query, context.CancellationToken);

        if (result.IsFailure)
        {
            var statusCode = result.Error.Code switch
            {
                "Merchant.NotFound" => StatusCode.NotFound,
                _ => StatusCode.Internal,
            };
            throw new RpcException(new Status(statusCode, result.Error.Message));
        }

        foreach (var item in result.Value.Items)
        {
            var response = new MerchantTransactionResponse
            {
                TransactionId = item.TransactionId.ToString(),
                Amount = new Money { Amount = item.Amount.ToString("F2"), Currency = item.Currency },
                Fee = new Money { Amount = item.Fee.ToString("F2"), Currency = item.Currency },
                Reference = item.Reference,
                PaymentMethod = item.Type,
                TerminalId = item.TerminalId ?? string.Empty,
                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc))
            };

            await responseStream.WriteAsync(response, context.CancellationToken);
        }
    }

    /// <summary>
    /// STORY-054: Generate a commission report for an agent-type merchant.
    /// </summary>
    public override async Task<MerchantCommissionResponse> GetCommissionReport(
        MerchantCommissionRequest request, ServerCallContext context)
    {
        var query = new GetMerchantCommissionQuery(
            MerchantId: Guid.Parse(request.MerchantId),
            DateFrom: request.DateRange?.From?.ToDateTime(),
            DateTo: request.DateRange?.To?.ToDateTime());

        var result = await _getCommissionHandler.HandleAsync(query, context.CancellationToken);

        if (result.IsFailure)
        {
            var statusCode = result.Error.Code switch
            {
                "Merchant.NotFound" => StatusCode.NotFound,
                "Merchant.NotAgent" => StatusCode.FailedPrecondition,
                _ => StatusCode.Internal,
            };
            throw new RpcException(new Status(statusCode, result.Error.Message));
        }

        var report = result.Value;
        var response = new MerchantCommissionResponse
        {
            MerchantId = report.MerchantId.ToString(),
            TotalCommission = new Money { Amount = report.TotalCommission.ToString("F2"), Currency = report.Currency },
            TotalTransactions = report.TotalTransactions
        };

        if (report.DateFrom.HasValue || report.DateTo.HasValue)
        {
            response.Period = new DateRange();
            if (report.DateFrom.HasValue)
                response.Period.From = Timestamp.FromDateTime(DateTime.SpecifyKind(report.DateFrom.Value, DateTimeKind.Utc));
            if (report.DateTo.HasValue)
                response.Period.To = Timestamp.FromDateTime(DateTime.SpecifyKind(report.DateTo.Value, DateTimeKind.Utc));
        }

        foreach (var lineItem in report.LineItems)
        {
            response.LineItems.Add(new CommissionLineItem
            {
                TransactionType = lineItem.TransactionType,
                TransactionCount = lineItem.TransactionCount,
                TotalTransactionAmount = new Money
                {
                    Amount = lineItem.TotalTransactionAmount.ToString("F2"),
                    Currency = lineItem.Currency
                },
                CommissionRate = lineItem.AverageCommissionRate.ToString("F4"),
                CommissionAmount = new Money
                {
                    Amount = lineItem.TotalCommissionAmount.ToString("F2"),
                    Currency = lineItem.Currency
                }
            });
        }

        return response;
    }

    private static Protos.Merchants.MerchantStatus MapStatus(string status) => status switch
    {
        "pending_kyc" or "pending" => Protos.Merchants.MerchantStatus.Pending,
        "active" => Protos.Merchants.MerchantStatus.Active,
        "suspended" => Protos.Merchants.MerchantStatus.Suspended,
        "closed" => Protos.Merchants.MerchantStatus.Closed,
        _ => Protos.Merchants.MerchantStatus.Unspecified,
    };

    private static string FormatAddress(MerchantAddress? address)
    {
        if (address is null) return string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(address.Line1)) parts.Add(address.Line1);
        if (!string.IsNullOrEmpty(address.Line2)) parts.Add(address.Line2);
        if (!string.IsNullOrEmpty(address.City)) parts.Add(address.City);
        if (!string.IsNullOrEmpty(address.Province)) parts.Add(address.Province);
        if (!string.IsNullOrEmpty(address.PostalCode)) parts.Add(address.PostalCode);
        if (!string.IsNullOrEmpty(address.CountryCode)) parts.Add(address.CountryCode);

        return string.Join(", ", parts);
    }
}
