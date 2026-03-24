using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using UniBank.Core.Modules.Agents.Application.Commands;
using UniBank.Core.Modules.Agents.Application.Handlers;
using UniBank.Protos.Agents;
using UniBank.Protos.Common;
using UniBank.SharedKernel.MultiTenancy;

namespace UniBank.Core.Modules.Agents.Grpc;

/// <summary>
/// gRPC service for agent cash-in, cash-out, float management, commission reporting,
/// and transaction receipts (STORY-032 through STORY-036).
/// </summary>
public sealed class AgentGrpcService : AgentService.AgentServiceBase
{
    private readonly CashInHandler _cashInHandler;
    private readonly CashOutHandler _cashOutHandler;
    private readonly GetFloatBalanceHandler _floatBalanceHandler;
    private readonly GetCommissionReportHandler _commissionReportHandler;
    private readonly GetTransactionReceiptHandler _receiptHandler;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<AgentGrpcService> _logger;

    public AgentGrpcService(
        CashInHandler cashInHandler,
        CashOutHandler cashOutHandler,
        GetFloatBalanceHandler floatBalanceHandler,
        GetCommissionReportHandler commissionReportHandler,
        GetTransactionReceiptHandler receiptHandler,
        ITenantProvider tenantProvider,
        ILogger<AgentGrpcService> logger)
    {
        _cashInHandler = cashInHandler;
        _cashOutHandler = cashOutHandler;
        _floatBalanceHandler = floatBalanceHandler;
        _commissionReportHandler = commissionReportHandler;
        _receiptHandler = receiptHandler;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public override async Task<CashOperationResponse> CashIn(
        CashInRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var amount = decimal.Parse(request.Amount.Amount);
        var currency = request.Amount.Currency;

        var command = new CashInCommand(
            AgentMerchantId: Guid.Parse(request.AgentId),
            CustomerPhone: request.CustomerPhone,
            Amount: amount,
            Currency: currency,
            AgentPin: request.AgentPin,
            TenantId: tenantId);

        var result = await _cashInHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
        {
            var statusCode = MapErrorToStatusCode(result.Error.Code);
            throw new RpcException(new Status(statusCode, result.Error.Message));
        }

        var value = result.Value;
        return new CashOperationResponse
        {
            Success = true,
            Message = "Cash-in completed successfully.",
            TransactionId = value.TransactionId.ToString(),
            Reference = value.Reference,
            Amount = new Money { Amount = value.Amount.ToString("F2"), Currency = value.Currency },
            Commission = new Money { Amount = value.Commission.ToString("F2"), Currency = value.Currency },
            NewFloatBalance = new Money { Amount = value.NewFloatBalance.ToString("F2"), Currency = value.Currency },
            CompletedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(value.CompletedAt, DateTimeKind.Utc)),
            CustomerFee = new Money { Amount = value.CustomerFee.ToString("F2"), Currency = value.Currency },
            Tax = new Money { Amount = value.Tax.ToString("F2"), Currency = value.Currency }
        };
    }

    public override async Task<CashOperationResponse> CashOut(
        CashOutRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var amount = decimal.Parse(request.Amount.Amount);
        var currency = request.Amount.Currency;

        var command = new CashOutCommand(
            AgentMerchantId: Guid.Parse(request.AgentId),
            CustomerAccountId: Guid.Parse(request.CustomerAccountId),
            Amount: amount,
            Currency: currency,
            CustomerPin: request.CustomerPin,
            AgentPin: request.AgentPin,
            TenantId: tenantId);

        var result = await _cashOutHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
        {
            var statusCode = MapErrorToStatusCode(result.Error.Code);
            throw new RpcException(new Status(statusCode, result.Error.Message));
        }

        var value = result.Value;
        return new CashOperationResponse
        {
            Success = true,
            Message = "Cash-out completed successfully.",
            TransactionId = value.TransactionId.ToString(),
            Reference = value.Reference,
            Amount = new Money { Amount = value.Amount.ToString("F2"), Currency = value.Currency },
            Commission = new Money { Amount = value.Commission.ToString("F2"), Currency = value.Currency },
            NewFloatBalance = new Money { Amount = value.NewFloatBalance.ToString("F2"), Currency = value.Currency },
            CompletedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(value.CompletedAt, DateTimeKind.Utc)),
            CustomerFee = new Money { Amount = value.CustomerFee.ToString("F2"), Currency = value.Currency },
            Tax = new Money { Amount = value.Tax.ToString("F2"), Currency = value.Currency }
        };
    }

    public override async Task<FloatBalanceResponse> GetFloatBalance(
        FloatBalanceRequest request, ServerCallContext context)
    {
        var agentMerchantId = Guid.Parse(request.AgentId);
        var result = await _floatBalanceHandler.HandleAsync(agentMerchantId, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.NotFound, result.Error.Message));

        var value = result.Value;
        return new FloatBalanceResponse
        {
            AgentId = request.AgentId,
            FloatBalance = new Money { Amount = value.FloatBalance.ToString("F2"), Currency = value.Currency },
            FloatLimit = new Money { Amount = value.FloatLimit.ToString("F2"), Currency = value.Currency },
            AvailableFloat = new Money { Amount = value.AvailableFloat.ToString("F2"), Currency = value.Currency }
        };
    }

    public override async Task<CommissionReportResponse> GetCommissionReport(
        CommissionReportRequest request, ServerCallContext context)
    {
        var agentMerchantId = Guid.Parse(request.AgentId);

        var from = request.DateRange.From.ToDateTime();
        var to = request.DateRange.To.ToDateTime();

        var result = await _commissionReportHandler.HandleAsync(
            agentMerchantId, from, to, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.NotFound, result.Error.Message));

        var value = result.Value;
        var response = new CommissionReportResponse
        {
            AgentId = request.AgentId,
            TotalCommission = new Money { Amount = value.TotalCommission.ToString("F2"), Currency = value.Currency },
            TotalTransactions = value.TotalTransactions
        };

        foreach (var item in value.Items)
        {
            response.Items.Add(new CommissionLineItem
            {
                TransactionType = item.TransactionType,
                Count = item.Count,
                TotalAmount = new Money { Amount = item.TotalAmount.ToString("F2"), Currency = item.Currency },
                TotalCommission = new Money { Amount = item.TotalCommission.ToString("F2"), Currency = item.Currency }
            });
        }

        return response;
    }

    public override async Task<TransactionReceiptResponse> GetTransactionReceipt(
        GetReceiptRequest request, ServerCallContext context)
    {
        var transactionId = Guid.Parse(request.TransactionId);
        var agentMerchantId = Guid.Parse(request.AgentId);

        var result = await _receiptHandler.HandleAsync(
            transactionId, agentMerchantId, context.CancellationToken);

        if (result.IsFailure)
        {
            var statusCode = MapErrorToStatusCode(result.Error.Code);
            throw new RpcException(new Status(statusCode, result.Error.Message));
        }

        var value = result.Value;
        return new TransactionReceiptResponse
        {
            ReceiptNumber = value.ReceiptNumber,
            TransactionType = value.TransactionType,
            CustomerPhone = value.CustomerPhone,
            Amount = new Money { Amount = value.Amount.ToString("F2"), Currency = value.Currency },
            Commission = new Money { Amount = value.Commission.ToString("F2"), Currency = value.Currency },
            NetAmount = new Money { Amount = value.NetAmount.ToString("F2"), Currency = value.Currency },
            AgentName = value.AgentName,
            Reference = value.Reference,
            Timestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(value.Timestamp, DateTimeKind.Utc)),
            Status = value.Status
        };
    }

    private static StatusCode MapErrorToStatusCode(string errorCode) => errorCode switch
    {
        "Agent.NotFound" or "Customer.NotFound" or "Agent.AccountNotFound"
            or "Transaction.NotFound" or "Commission.NotFound" or "Agent.NoFloat"
            => StatusCode.NotFound,
        "Agent.Inactive" or "Customer.Inactive" or "Agent.NoPinSet" or "Customer.NoPinSet"
            => StatusCode.FailedPrecondition,
        "Agent.InvalidPin" or "Customer.InvalidPin"
            => StatusCode.Unauthenticated,
        "Agent.InsufficientFloat" or "Customer.InsufficientFunds" or "Agent.InvalidAmount"
            => StatusCode.FailedPrecondition,
        _ => StatusCode.Internal,
    };
}
