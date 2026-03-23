using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using UniBank.Core.Modules.CardTransactions.Application.Commands;
using UniBank.Core.Modules.CardTransactions.Application.Handlers;
using UniBank.Core.Modules.CardTransactions.Application.Validators;
using UniBank.Protos.CardTransactions;
using UniBank.SharedKernel.MultiTenancy;

namespace UniBank.Core.Modules.CardTransactions.Grpc;

/// <summary>
/// gRPC service implementation for card transaction processing (EPIC-015).
/// Routes incoming switch calls to the appropriate handler for purchases,
/// deposits, balance enquiries, and statement enquiries.
/// </summary>
public sealed class CardTransactionGrpcService : CardTransactionService.CardTransactionServiceBase
{
    private readonly ProcessPurchaseHandler _purchaseHandler;
    private readonly ProcessDepositHandler _depositHandler;
    private readonly BalanceEnquiryHandler _balanceEnquiryHandler;
    private readonly StatementEnquiryHandler _statementEnquiryHandler;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<CardTransactionGrpcService> _logger;

    public CardTransactionGrpcService(
        ProcessPurchaseHandler purchaseHandler,
        ProcessDepositHandler depositHandler,
        BalanceEnquiryHandler balanceEnquiryHandler,
        StatementEnquiryHandler statementEnquiryHandler,
        ITenantProvider tenantProvider,
        ILogger<CardTransactionGrpcService> logger)
    {
        _purchaseHandler = purchaseHandler;
        _depositHandler = depositHandler;
        _balanceEnquiryHandler = balanceEnquiryHandler;
        _statementEnquiryHandler = statementEnquiryHandler;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public override async Task<CardTransactionResponse> ProcessPurchase(
        PurchaseRequest request, ServerCallContext context)
    {
        var tenantId = ResolveTenantId(request.TenantId);

        if (string.IsNullOrWhiteSpace(request.CardHolderAccount))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "card_holder_account is required."));

        if (request.Amount is null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "amount is required."));

        var amount = ParseMoney(request.Amount);
        var currency = string.IsNullOrWhiteSpace(request.Amount.Currency) ? "ZWG" : request.Amount.Currency;

        var command = new ProcessPurchaseCommand(
            TransactionId: request.TransactionId,
            CardHolderAccount: request.CardHolderAccount,
            MerchantId: request.MerchantId,
            MerchantName: request.MerchantName,
            TerminalId: request.TerminalId,
            Amount: amount,
            Currency: currency,
            ProcessingCode: request.ProcessingCode,
            SourceInstitution: request.SourceInstitution,
            AcquiringInstitution: request.AcquiringInstitution,
            Stan: request.Stan,
            RetrievalReference: request.RetrievalReference,
            IsOnUs: request.IsOnUs,
            TenantId: tenantId);

        var result = await _purchaseHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            return MapFailureToResponse(result.Error, request.TransactionId, currency);

        return MapToCardTransactionResponse(result.Value);
    }

    public override async Task<CardTransactionResponse> ProcessDeposit(
        DepositRequest request, ServerCallContext context)
    {
        var tenantId = ResolveTenantId(request.TenantId);

        if (string.IsNullOrWhiteSpace(request.CardHolderAccount))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "card_holder_account is required."));

        if (request.Amount is null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "amount is required."));

        var amount = ParseMoney(request.Amount);
        var currency = string.IsNullOrWhiteSpace(request.Amount.Currency) ? "ZWG" : request.Amount.Currency;

        var command = new ProcessDepositCommand(
            TransactionId: request.TransactionId,
            CardHolderAccount: request.CardHolderAccount,
            MerchantId: request.MerchantId,
            MerchantName: request.MerchantName,
            TerminalId: request.TerminalId,
            Amount: amount,
            Currency: currency,
            ProcessingCode: request.ProcessingCode,
            SourceInstitution: request.SourceInstitution,
            AcquiringInstitution: request.AcquiringInstitution,
            Stan: request.Stan,
            RetrievalReference: request.RetrievalReference,
            IsOnUs: request.IsOnUs,
            TenantId: tenantId);

        var result = await _depositHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            return MapFailureToResponse(result.Error, request.TransactionId, currency);

        return MapToCardTransactionResponse(result.Value);
    }

    public override async Task<BalanceEnquiryResponse> BalanceEnquiry(
        BalanceEnquiryRequest request, ServerCallContext context)
    {
        var tenantId = ResolveTenantId(request.TenantId);

        if (string.IsNullOrWhiteSpace(request.CardHolderAccount))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "card_holder_account is required."));

        var command = new BalanceEnquiryCommand(
            TransactionId: request.TransactionId,
            CardHolderAccount: request.CardHolderAccount,
            TerminalId: request.TerminalId,
            SourceInstitution: request.SourceInstitution,
            Stan: request.Stan,
            RetrievalReference: request.RetrievalReference,
            IsOnUs: request.IsOnUs,
            TenantId: tenantId);

        var result = await _balanceEnquiryHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
        {
            var responseCode = CardTransactionValidator.MapToResponseCode(result.Error.Code);
            return new BalanceEnquiryResponse
            {
                Success = false,
                ResponseCode = responseCode,
                Message = result.Error.Message,
                TransactionId = request.TransactionId
            };
        }

        var value = result.Value;
        return new BalanceEnquiryResponse
        {
            Success = true,
            ResponseCode = value.ResponseCode,
            Message = value.Message,
            AvailableBalance = new UniBank.Protos.Common.Money
            {
                Amount = value.AvailableBalance.ToString("F2"),
                Currency = value.Currency
            },
            LedgerBalance = new UniBank.Protos.Common.Money
            {
                Amount = value.LedgerBalance.ToString("F2"),
                Currency = value.Currency
            },
            TransactionId = value.TransactionId
        };
    }

    public override async Task<StatementEnquiryResponse> StatementEnquiry(
        StatementEnquiryRequest request, ServerCallContext context)
    {
        var tenantId = ResolveTenantId(request.TenantId);

        if (string.IsNullOrWhiteSpace(request.CardHolderAccount))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "card_holder_account is required."));

        var command = new StatementEnquiryCommand(
            TransactionId: request.TransactionId,
            CardHolderAccount: request.CardHolderAccount,
            TerminalId: request.TerminalId,
            SourceInstitution: request.SourceInstitution,
            Stan: request.Stan,
            RetrievalReference: request.RetrievalReference,
            MaxRecords: request.MaxRecords,
            IsOnUs: request.IsOnUs,
            TenantId: tenantId);

        var result = await _statementEnquiryHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
        {
            var responseCode = CardTransactionValidator.MapToResponseCode(result.Error.Code);
            return new StatementEnquiryResponse
            {
                Success = false,
                ResponseCode = responseCode,
                Message = result.Error.Message,
                TransactionId = request.TransactionId
            };
        }

        var value = result.Value;
        var response = new StatementEnquiryResponse
        {
            Success = true,
            ResponseCode = value.ResponseCode,
            Message = value.Message,
            AvailableBalance = new UniBank.Protos.Common.Money
            {
                Amount = value.AvailableBalance.ToString("F2"),
                Currency = value.Currency
            },
            TransactionId = value.TransactionId
        };

        foreach (var entry in value.Entries)
        {
            response.Entries.Add(new StatementEntry
            {
                Date = Timestamp.FromDateTime(DateTime.SpecifyKind(entry.Date, DateTimeKind.Utc)),
                Description = entry.Description,
                Amount = new UniBank.Protos.Common.Money
                {
                    Amount = entry.Amount.ToString("F2"),
                    Currency = entry.Currency
                },
                Type = entry.Type,
                Reference = entry.Reference,
                BalanceAfter = new UniBank.Protos.Common.Money
                {
                    Amount = entry.BalanceAfter.ToString("F2"),
                    Currency = entry.Currency
                }
            });
        }

        return response;
    }

    private string ResolveTenantId(string requestTenantId)
    {
        if (!string.IsNullOrWhiteSpace(requestTenantId))
            return requestTenantId;

        return _tenantProvider.GetTenantId();
    }

    private static decimal ParseMoney(UniBank.Protos.Common.Money money)
    {
        if (decimal.TryParse(money.Amount, out var amount))
            return amount;

        throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid amount format."));
    }

    private static CardTransactionResponse MapFailureToResponse(
        UniBank.SharedKernel.Results.Error error, string transactionId, string currency)
    {
        var responseCode = CardTransactionValidator.MapToResponseCode(error.Code);
        return new CardTransactionResponse
        {
            Success = false,
            ResponseCode = responseCode,
            Message = error.Message,
            TransactionId = transactionId,
            ProcessedAt = Timestamp.FromDateTime(DateTime.UtcNow)
        };
    }

    private static CardTransactionResponse MapToCardTransactionResponse(CardTransactionResult value)
    {
        var response = new CardTransactionResponse
        {
            Success = value.Success,
            ResponseCode = value.ResponseCode,
            AuthorizationCode = value.AuthorizationCode ?? "",
            Message = value.Message,
            AvailableBalance = new UniBank.Protos.Common.Money
            {
                Amount = value.AvailableBalance.ToString("F2"),
                Currency = value.Currency
            },
            TransactionId = value.TransactionId
        };

        if (value.ProcessedAt.HasValue)
        {
            response.ProcessedAt = Timestamp.FromDateTime(
                DateTime.SpecifyKind(value.ProcessedAt.Value, DateTimeKind.Utc));
        }

        return response;
    }
}
