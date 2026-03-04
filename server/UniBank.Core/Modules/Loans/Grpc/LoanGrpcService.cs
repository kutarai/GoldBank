using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using UniBank.Core.Modules.Loans.Application.Commands;
using UniBank.Core.Modules.Loans.Application.Handlers;
using UniBank.Protos.Loans;
using UniBank.SharedKernel.MultiTenancy;

namespace UniBank.Core.Modules.Loans.Grpc;

/// <summary>
/// gRPC service implementation for loan operations.
/// Maps proto-defined RPCs to application-layer handlers.
/// </summary>
public sealed class LoanGrpcService : LoanService.LoanServiceBase
{
    private readonly ApplyForLoanHandler _applyHandler;
    private readonly GetLoanHandler _getLoanHandler;
    private readonly ListLoansHandler _listLoansHandler;
    private readonly GetLoanScheduleHandler _getScheduleHandler;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<LoanGrpcService> _logger;

    public LoanGrpcService(
        ApplyForLoanHandler applyHandler,
        GetLoanHandler getLoanHandler,
        ListLoansHandler listLoansHandler,
        GetLoanScheduleHandler getScheduleHandler,
        ITenantProvider tenantProvider,
        ILogger<LoanGrpcService> logger)
    {
        _applyHandler = applyHandler;
        _getLoanHandler = getLoanHandler;
        _listLoansHandler = listLoansHandler;
        _getScheduleHandler = getScheduleHandler;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public override async Task<LoanApplicationResponse> ApplyForLoan(
        ApplyForLoanRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();

        if (string.IsNullOrWhiteSpace(request.AccountId) ||
            !Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        if (request.Amount is null ||
            !decimal.TryParse(request.Amount.Amount, out var amount) || amount <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid positive amount is required."));

        var currency = request.Amount.Currency;
        if (string.IsNullOrWhiteSpace(currency))
            currency = "ZWG";

        if (request.TenureMonths <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid tenure_months is required."));

        if (string.IsNullOrWhiteSpace(request.Purpose))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "purpose is required."));

        if (string.IsNullOrWhiteSpace(request.Pin))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "pin is required."));

        _logger.LogInformation(
            "ApplyForLoan request: account {AccountId}, amount {Amount} {Currency}, tenure {Tenure}mo",
            accountId, amount, currency, request.TenureMonths);

        var command = new ApplyForLoanCommand(
            AccountId: accountId,
            Amount: amount,
            Currency: currency,
            TenureMonths: request.TenureMonths,
            Purpose: request.Purpose,
            Pin: request.Pin,
            TenantId: tenantId);

        var result = await _applyHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(MapErrorToStatusCode(result.Error.Code), result.Error.Message));

        var v = result.Value;
        return new LoanApplicationResponse
        {
            Success = true,
            Message = v.Status == "disbursed"
                ? "Loan approved and disbursed successfully."
                : "Loan application was not approved.",
            LoanId = v.LoanId,
            Reference = v.Reference,
            Status = MapToProtoStatus(v.Status),
            Principal = new UniBank.Protos.Common.Money { Amount = v.Principal.ToString("F2"), Currency = v.Currency },
            InterestRate = v.InterestRate.ToString("P2"),
            MonthlyPayment = new UniBank.Protos.Common.Money { Amount = v.MonthlyPayment.ToString("F2"), Currency = v.Currency },
            TenureMonths = v.TenureMonths,
            CreditScore = v.CreditScore,
            NewBalance = new UniBank.Protos.Common.Money { Amount = v.NewBalance.ToString("F2"), Currency = v.Currency },
        };
    }

    public override async Task<LoanDetailResponse> GetLoan(
        GetLoanRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.LoanId, out var loanId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid loan_id is required."));

        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        var result = await _getLoanHandler.HandleAsync(loanId, accountId, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.NotFound, result.Error.Message));

        var loan = result.Value;
        var response = new LoanDetailResponse
        {
            LoanId = loan.Id.ToString(),
            Reference = loan.Reference,
            Status = MapToProtoStatus(loan.Status),
            Principal = new UniBank.Protos.Common.Money { Amount = loan.Principal.ToString("F2"), Currency = loan.Currency },
            OutstandingBalance = new UniBank.Protos.Common.Money { Amount = loan.OutstandingBalance.ToString("F2"), Currency = loan.Currency },
            InterestRate = loan.InterestRate.ToString("P2"),
            TenureMonths = loan.TenureMonths,
            MonthlyPayment = new UniBank.Protos.Common.Money { Amount = loan.MonthlyPayment.ToString("F2"), Currency = loan.Currency },
            Purpose = loan.Purpose,
            PaymentsMade = loan.PaymentsMade,
            TotalPayments = loan.TenureMonths,
            CreditScore = loan.CreditScore,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(loan.CreatedAt, DateTimeKind.Utc)),
        };

        // Find next unpaid payment
        var nextPayment = loan.Payments.FirstOrDefault(p => !p.IsPaid);
        if (nextPayment is not null)
        {
            response.NextPaymentDate = Timestamp.FromDateTime(
                DateTime.SpecifyKind(nextPayment.DueDate, DateTimeKind.Utc));
        }

        return response;
    }

    public override async Task<ListLoansResponse> ListLoans(
        ListLoansRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        var result = await _listLoansHandler.HandleAsync(
            accountId, request.StatusFilter, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        var response = new ListLoansResponse();
        foreach (var loan in result.Value)
        {
            response.Loans.Add(new LoanSummary
            {
                LoanId = loan.Id.ToString(),
                Reference = loan.Reference,
                Status = MapToProtoStatus(loan.Status),
                Principal = new UniBank.Protos.Common.Money { Amount = loan.Principal.ToString("F2"), Currency = loan.Currency },
                OutstandingBalance = new UniBank.Protos.Common.Money { Amount = loan.OutstandingBalance.ToString("F2"), Currency = loan.Currency },
                MonthlyPayment = new UniBank.Protos.Common.Money { Amount = loan.MonthlyPayment.ToString("F2"), Currency = loan.Currency },
                PaymentsMade = loan.PaymentsMade,
                TotalPayments = loan.TenureMonths,
                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(loan.CreatedAt, DateTimeKind.Utc)),
            });
        }

        return response;
    }

    public override async Task<LoanScheduleResponse> GetLoanSchedule(
        GetLoanScheduleRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.LoanId, out var loanId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid loan_id is required."));

        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        var result = await _getScheduleHandler.HandleAsync(loanId, accountId, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.NotFound, result.Error.Message));

        var response = new LoanScheduleResponse();
        foreach (var p in result.Value)
        {
            // Use the loan's currency from the parent loan (retrieved via schedule handler)
            var currency = "ZWG"; // Default — schedule entries don't carry currency individually
            response.Entries.Add(new LoanScheduleEntry
            {
                PaymentNumber = p.PaymentNumber,
                PrincipalAmount = new UniBank.Protos.Common.Money { Amount = p.PrincipalAmount.ToString("F2"), Currency = currency },
                InterestAmount = new UniBank.Protos.Common.Money { Amount = p.InterestAmount.ToString("F2"), Currency = currency },
                TotalPayment = new UniBank.Protos.Common.Money { Amount = p.TotalPayment.ToString("F2"), Currency = currency },
                RemainingBalance = new UniBank.Protos.Common.Money { Amount = p.RemainingBalance.ToString("F2"), Currency = currency },
                DueDate = Timestamp.FromDateTime(DateTime.SpecifyKind(p.DueDate, DateTimeKind.Utc)),
                IsPaid = p.IsPaid,
            });
        }

        return response;
    }

    private static LoanStatus MapToProtoStatus(string status)
    {
        return status switch
        {
            "pending" => LoanStatus.Pending,
            "approved" => LoanStatus.Approved,
            "rejected" => LoanStatus.Rejected,
            "disbursed" => LoanStatus.Disbursed,
            "repaying" => LoanStatus.Repaying,
            "paid_off" => LoanStatus.PaidOff,
            "defaulted" => LoanStatus.Defaulted,
            _ => LoanStatus.Unspecified,
        };
    }

    private static StatusCode MapErrorToStatusCode(string errorCode)
    {
        return errorCode switch
        {
            "Account.NotFound" => StatusCode.NotFound,
            "Account.Inactive" => StatusCode.FailedPrecondition,
            "Account.NoPinSet" => StatusCode.FailedPrecondition,
            "Auth.InvalidPIN" => StatusCode.Unauthenticated,
            "Loan.InvalidAmount" => StatusCode.InvalidArgument,
            "Loan.InvalidTenure" => StatusCode.InvalidArgument,
            "Loan.InsufficientKyc" => StatusCode.FailedPrecondition,
            "Loan.DefaultedLoan" => StatusCode.FailedPrecondition,
            "Loan.NotFound" => StatusCode.NotFound,
            _ => StatusCode.Internal,
        };
    }
}
