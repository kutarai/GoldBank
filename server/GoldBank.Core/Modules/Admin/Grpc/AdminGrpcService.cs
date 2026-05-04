using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Admin.Application.Handlers;
using GoldBank.Core.Modules.Admin.Domain.Entities;
using GoldBank.Core.Modules.FraudDetection.Application.Handlers;
using GoldBank.Protos.Admin;
using GoldBank.Protos.Common;
using ProtoFraudAlertSummary = GoldBank.Protos.Admin.FraudAlertSummary;

namespace GoldBank.Core.Modules.Admin.Grpc;

/// <summary>
/// gRPC service implementation for all admin back-office operations.
/// Covers: STORY-055 through STORY-061, plus STORY-072 fraud detection.
/// </summary>
public sealed class AdminGrpcService : AdminService.AdminServiceBase
{
    private readonly SearchCustomersHandler _searchCustomersHandler;
    private readonly ManageAccountHandler _manageAccountHandler;
    private readonly ManageMerchantHandler _manageMerchantHandler;
    private readonly SearchTransactionsHandler _searchTransactionsHandler;
    private readonly ReviewKycHandler _reviewKycHandler;
    private readonly UpdateSystemConfigHandler _updateSystemConfigHandler;
    private readonly CreateDisputeHandler _createDisputeHandler;
    private readonly ResolveDisputeHandler _resolveDisputeHandler;
    private readonly GetFraudAlertsHandler _getFraudAlertsHandler;
    private readonly ReviewFraudAlertHandler _reviewFraudAlertHandler;
    private readonly GoldBankDbContext _dbContext;

    public AdminGrpcService(
        SearchCustomersHandler searchCustomersHandler,
        ManageAccountHandler manageAccountHandler,
        ManageMerchantHandler manageMerchantHandler,
        SearchTransactionsHandler searchTransactionsHandler,
        ReviewKycHandler reviewKycHandler,
        UpdateSystemConfigHandler updateSystemConfigHandler,
        CreateDisputeHandler createDisputeHandler,
        ResolveDisputeHandler resolveDisputeHandler,
        GetFraudAlertsHandler getFraudAlertsHandler,
        ReviewFraudAlertHandler reviewFraudAlertHandler,
        GoldBankDbContext dbContext)
    {
        _searchCustomersHandler = searchCustomersHandler;
        _manageAccountHandler = manageAccountHandler;
        _manageMerchantHandler = manageMerchantHandler;
        _searchTransactionsHandler = searchTransactionsHandler;
        _reviewKycHandler = reviewKycHandler;
        _updateSystemConfigHandler = updateSystemConfigHandler;
        _createDisputeHandler = createDisputeHandler;
        _resolveDisputeHandler = resolveDisputeHandler;
        _getFraudAlertsHandler = getFraudAlertsHandler;
        _reviewFraudAlertHandler = reviewFraudAlertHandler;
        _dbContext = dbContext;
    }

    // ── STORY-056: Customer Search ──────────────────────────────────────

    public override async Task<SearchCustomersResponse> SearchCustomers(
        SearchCustomersRequest request, ServerCallContext context)
    {
        var page = request.Pagination?.Page ?? 1;
        var pageSize = request.Pagination?.PageSize ?? 20;

        var result = await _searchCustomersHandler.HandleAsync(
            request.Query,
            request.StatusFilter,
            page,
            pageSize,
            context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        var response = new SearchCustomersResponse
        {
            Pagination = new PaginationResponse
            {
                TotalCount = result.Value.TotalCount,
                Page = result.Value.Page,
                PageSize = result.Value.PageSize,
                TotalPages = (int)Math.Ceiling((double)result.Value.TotalCount / result.Value.PageSize),
                HasNext = result.Value.Page * result.Value.PageSize < result.Value.TotalCount,
                HasPrevious = result.Value.Page > 1
            }
        };

        foreach (var c in result.Value.Customers)
        {
            var summary = new CustomerSummary
            {
                AccountId = c.AccountId,
                PhoneNumber = c.PhoneNumber,
                FullName = c.FullName,
                Status = c.Status,
                KycLevel = c.KycLevel,
                Balance = new Money { Amount = c.Balance.ToString("F2"), Currency = c.Currency },
                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc))
            };

            if (c.LastLoginAt.HasValue)
            {
                summary.LastLoginAt = Timestamp.FromDateTime(
                    DateTime.SpecifyKind(c.LastLoginAt.Value, DateTimeKind.Utc));
            }

            response.Customers.Add(summary);
        }

        return response;
    }

    // ── STORY-056: Account Management ───────────────────────────────────

    public override async Task<StatusResponse> ManageAccount(
        ManageAccountRequest request, ServerCallContext context)
    {
        var actionStr = request.Action switch
        {
            AccountAction.Suspend => "SUSPEND",
            AccountAction.Activate => "ACTIVATE",
            AccountAction.Close => "CLOSE",
            AccountAction.Freeze => "FREEZE",
            AccountAction.Unfreeze => "UNFREEZE",
            AccountAction.ResetPin => "RESET_PIN",
            _ => "UNKNOWN"
        };

        var result = await _manageAccountHandler.HandleAsync(
            request.AccountId,
            actionStr,
            request.Reason,
            request.AdminId,
            context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        return new StatusResponse { Success = true, Message = $"Account {actionStr.ToLowerInvariant()} successfully." };
    }

    // ── STORY-057: Merchant Management ──────────────────────────────────

    public override async Task<StatusResponse> ManageMerchant(
        ManageMerchantRequest request, ServerCallContext context)
    {
        var actionStr = request.Action switch
        {
            MerchantAction.Approve => "APPROVE",
            MerchantAction.Suspend => "SUSPEND",
            MerchantAction.Activate => "ACTIVATE",
            MerchantAction.Close => "CLOSE",
            _ => "UNKNOWN"
        };

        var result = await _manageMerchantHandler.HandleAsync(
            request.MerchantId,
            actionStr,
            request.Reason,
            request.AdminId,
            context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        return new StatusResponse { Success = true, Message = $"Merchant {actionStr.ToLowerInvariant()} successfully." };
    }

    // ── STORY-058: Transaction Search (streaming) ───────────────────────

    public override async Task SearchTransactions(
        SearchTransactionsRequest request,
        IServerStreamWriter<AdminTransactionResponse> responseStream,
        ServerCallContext context)
    {
        var page = request.Pagination?.Page ?? 1;
        var pageSize = request.Pagination?.PageSize ?? 50;

        decimal? minAmount = null;
        if (request.MinAmount is not null && decimal.TryParse(request.MinAmount.Amount, out var min))
            minAmount = min;

        decimal? maxAmount = null;
        if (request.MaxAmount is not null && decimal.TryParse(request.MaxAmount.Amount, out var max))
            maxAmount = max;

        var result = await _searchTransactionsHandler.HandleAsync(
            request.AccountId,
            request.MerchantId,
            request.Reference,
            request.TypeFilter,
            request.StatusFilter,
            request.DateRange?.From?.ToDateTime(),
            request.DateRange?.To?.ToDateTime(),
            minAmount,
            maxAmount,
            page,
            pageSize,
            context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        foreach (var tx in result.Value)
        {
            await responseStream.WriteAsync(new AdminTransactionResponse
            {
                TransactionId = tx.TransactionId,
                AccountId = tx.AccountId,
                AccountPhone = tx.AccountPhone,
                Type = tx.Type,
                Amount = new Money { Amount = tx.Amount.ToString("F2"), Currency = tx.Currency },
                Fee = new Money { Amount = tx.Fee.ToString("F2"), Currency = tx.Currency },
                Status = tx.Status,
                Reference = tx.Reference,
                CounterpartyInfo = tx.CounterpartyInfo,
                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(tx.CreatedAt, DateTimeKind.Utc))
            }, context.CancellationToken);
        }
    }

    // ── STORY-059: KYC Review ───────────────────────────────────────────

    public override async Task<StatusResponse> ReviewKYC(
        ReviewKYCRequest request, ServerCallContext context)
    {
        var decisionStr = request.Decision switch
        {
            KYCDecision.Approve => "APPROVE",
            KYCDecision.Reject => "REJECT",
            KYCDecision.RequestResubmit => "REQUEST_RESUBMIT",
            _ => "UNKNOWN"
        };

        var result = await _reviewKycHandler.HandleAsync(
            request.DocumentId,
            decisionStr,
            request.Notes,
            request.AdminId,
            context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        return new StatusResponse { Success = true, Message = $"KYC document {decisionStr.ToLowerInvariant()} successfully." };
    }

    // ── STORY-060: System Config ────────────────────────────────────────

    public override async Task<StatusResponse> UpdateSystemConfig(
        UpdateSystemConfigRequest request, ServerCallContext context)
    {
        var result = await _updateSystemConfigHandler.HandleAsync(
            request.Key,
            request.ValueJson,
            string.IsNullOrWhiteSpace(request.TenantId) ? null : request.TenantId,
            request.AdminId,
            context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        return new StatusResponse { Success = true, Message = "System configuration updated successfully." };
    }

    // ── STORY-061: Disputes ─────────────────────────────────────────────

    public override async Task<DisputeResponse> CreateDispute(
        CreateDisputeRequest request, ServerCallContext context)
    {
        var result = await _createDisputeHandler.HandleAsync(
            request.TransactionId,
            request.AccountId,
            request.Type,
            request.Description,
            request.AdminId,
            context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        return MapDisputeToResponse(result.Value);
    }

    public override async Task<DisputeResponse> ResolveDispute(
        ResolveDisputeRequest request, ServerCallContext context)
    {
        decimal? refundAmount = null;
        string? refundCurrency = null;
        if (request.RefundAmount is not null && decimal.TryParse(request.RefundAmount.Amount, out var amount))
        {
            refundAmount = amount;
            refundCurrency = string.IsNullOrWhiteSpace(request.RefundAmount.Currency)
                ? "ZWG"
                : request.RefundAmount.Currency;
        }

        var result = await _resolveDisputeHandler.HandleAsync(
            request.DisputeId,
            request.Resolution,
            request.Status,
            refundAmount,
            refundCurrency,
            request.AdminId,
            context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        return MapDisputeToResponse(result.Value);
    }

    public override async Task<ListDisputesResponse> ListDisputes(
        ListDisputesRequest request, ServerCallContext context)
    {
        var queryable = _dbContext.Set<Dispute>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.AccountId) && Guid.TryParse(request.AccountId, out var accountGuid))
        {
            queryable = queryable.Where(d => d.AccountId == accountGuid);
        }

        if (!string.IsNullOrWhiteSpace(request.StatusFilter) &&
            System.Enum.TryParse<DisputeStatus>(request.StatusFilter, true, out var statusFilter))
        {
            queryable = queryable.Where(d => d.Status == statusFilter);
        }

        var page = request.Pagination?.Page ?? 1;
        var pageSize = Math.Clamp(request.Pagination?.PageSize ?? 20, 1, 100);
        var totalCount = await queryable.CountAsync(context.CancellationToken);

        var disputes = await queryable
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(context.CancellationToken);

        var response = new ListDisputesResponse
        {
            Pagination = new PaginationResponse
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                HasNext = page * pageSize < totalCount,
                HasPrevious = page > 1
            }
        };

        foreach (var d in disputes)
        {
            response.Disputes.Add(MapDisputeToResponse(CreateDisputeHandler.MapToDto(d)));
        }

        return response;
    }

    // ── STORY-072: Fraud Detection ──────────────────────────────────────

    public override async Task<GetFraudAlertsResponse> GetFraudAlerts(
        GetFraudAlertsRequest request, ServerCallContext context)
    {
        var query = new GetFraudAlertsQuery(
            StatusFilter: request.StatusFilter,
            SeverityFilter: request.SeverityFilter,
            DateFrom: request.DateRange?.From?.ToDateTime(),
            DateTo: request.DateRange?.To?.ToDateTime(),
            TenantId: null,
            Page: request.Pagination?.Page ?? 1,
            PageSize: request.Pagination?.PageSize ?? 20);

        var result = await _getFraudAlertsHandler.HandleAsync(query, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        var response = new GetFraudAlertsResponse
        {
            Pagination = new PaginationResponse
            {
                TotalCount = result.Value.TotalCount,
                Page = result.Value.Page,
                PageSize = result.Value.PageSize,
                TotalPages = result.Value.TotalPages,
                HasNext = result.Value.Page < result.Value.TotalPages,
                HasPrevious = result.Value.Page > 1
            }
        };

        foreach (var a in result.Value.Alerts)
        {
            response.Alerts.Add(new ProtoFraudAlertSummary
            {
                AlertId = a.AlertId,
                AccountId = a.AccountId,
                TransactionId = a.TransactionId,
                AlertType = a.AlertType,
                Severity = a.Severity,
                Description = a.Description,
                Status = a.Status,
                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc))
            });
        }

        return response;
    }

    public override async Task<StatusResponse> ReviewFraudAlert(
        ReviewFraudAlertRequest request, ServerCallContext context)
    {
        var command = new ReviewFraudAlertCommand(
            AlertId: request.AlertId,
            AdminId: request.AdminId,
            Decision: request.Decision,
            Notes: request.Notes,
            SuspendAccount: request.SuspendAccount);

        var result = await _reviewFraudAlertHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        return new StatusResponse { Success = true, Message = "Fraud alert reviewed successfully." };
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static DisputeResponse MapDisputeToResponse(DisputeDto dto)
    {
        var response = new DisputeResponse
        {
            DisputeId = dto.DisputeId,
            TransactionId = dto.TransactionId,
            AccountId = dto.AccountId,
            Type = dto.Type,
            Description = dto.Description,
            Status = dto.Status,
            Resolution = dto.Resolution ?? string.Empty,
            AdminUserId = dto.AdminUserId ?? string.Empty,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(dto.CreatedAt, DateTimeKind.Utc))
        };

        if (dto.RefundAmount.HasValue)
        {
            response.RefundAmount = new Money
            {
                Amount = dto.RefundAmount.Value.ToString("F2"),
                Currency = dto.RefundCurrency
            };
        }

        if (dto.ResolvedAt.HasValue)
        {
            response.ResolvedAt = Timestamp.FromDateTime(
                DateTime.SpecifyKind(dto.ResolvedAt.Value, DateTimeKind.Utc));
        }

        return response;
    }
}
