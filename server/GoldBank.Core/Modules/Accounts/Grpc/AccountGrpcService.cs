using FluentValidation;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Modules.Accounts.Application.Commands;
using GoldBank.Core.Modules.Accounts.Application.Handlers;
using GoldBank.Core.Modules.Accounts.Infrastructure.Services;
using GoldBank.Protos.Accounts;
using GoldBank.SharedKernel.MultiTenancy;

namespace GoldBank.Core.Modules.Accounts.Grpc;

/// <summary>
/// gRPC service implementation for all account operations.
/// Maps proto-defined RPCs to application-layer handlers.
/// Covers: Registration (STORY-009), Auth (STORY-018), Device Transfer (STORY-014).
/// </summary>
public sealed class AccountGrpcService : AccountService.AccountServiceBase
{
    private readonly RegisterHandler _registerHandler;
    private readonly VerifyOtpHandler _verifyOtpHandler;
    private readonly CreatePINHandler _createPinHandler;
    private readonly AuthenticateHandler _authenticateHandler;
    private readonly RefreshTokenHandler _refreshTokenHandler;
    private readonly LogoutHandler _logoutHandler;
    private readonly DeviceTransferHandler _transferHandler;
    private readonly GetProfileHandler _getProfileHandler;
    private readonly UpdateProfileHandler _updateProfileHandler;
    private readonly GetBalanceHandler _getBalanceHandler;
    private readonly GetTransactionsHandler _getTransactionsHandler;
    private readonly IValidator<RegisterCommand> _registerValidator;
    private readonly IValidator<VerifyOtpCommand> _verifyOtpValidator;
    private readonly IValidator<AuthenticateCommand> _authenticateValidator;
    private readonly IValidator<InitiateDeviceTransferCommand> _initiateTransferValidator;
    private readonly IValidator<CompleteDeviceTransferCommand> _completeTransferValidator;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<AccountGrpcService> _logger;

    public AccountGrpcService(
        RegisterHandler registerHandler,
        VerifyOtpHandler verifyOtpHandler,
        CreatePINHandler createPinHandler,
        AuthenticateHandler authenticateHandler,
        RefreshTokenHandler refreshTokenHandler,
        LogoutHandler logoutHandler,
        DeviceTransferHandler transferHandler,
        GetProfileHandler getProfileHandler,
        UpdateProfileHandler updateProfileHandler,
        GetBalanceHandler getBalanceHandler,
        GetTransactionsHandler getTransactionsHandler,
        IValidator<RegisterCommand> registerValidator,
        IValidator<VerifyOtpCommand> verifyOtpValidator,
        IValidator<AuthenticateCommand> authenticateValidator,
        IValidator<InitiateDeviceTransferCommand> initiateTransferValidator,
        IValidator<CompleteDeviceTransferCommand> completeTransferValidator,
        ITenantProvider tenantProvider,
        ILogger<AccountGrpcService> logger)
    {
        _registerHandler = registerHandler;
        _verifyOtpHandler = verifyOtpHandler;
        _createPinHandler = createPinHandler;
        _authenticateHandler = authenticateHandler;
        _refreshTokenHandler = refreshTokenHandler;
        _logoutHandler = logoutHandler;
        _transferHandler = transferHandler;
        _getProfileHandler = getProfileHandler;
        _updateProfileHandler = updateProfileHandler;
        _getBalanceHandler = getBalanceHandler;
        _getTransactionsHandler = getTransactionsHandler;
        _registerValidator = registerValidator;
        _verifyOtpValidator = verifyOtpValidator;
        _authenticateValidator = authenticateValidator;
        _initiateTransferValidator = initiateTransferValidator;
        _completeTransferValidator = completeTransferValidator;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    // ── Registration (STORY-009) ─────────────────────────────────────

    public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        var tenantId = ResolveTenantId(request.TenantId);

        var command = new RegisterCommand(
            PhoneNumber: request.PhoneNumber,
            DeviceId: request.DeviceId,
            TenantId: tenantId);

        var validation = await _registerValidator.ValidateAsync(command, context.CancellationToken);
        if (!validation.IsValid)
        {
            var errorMessage = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            throw new RpcException(new Status(StatusCode.InvalidArgument, errorMessage));
        }

        var result = await _registerHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(MapErrorToStatusCode(result.Error.Code), result.Error.Message));

        return new RegisterResponse
        {
            Success = true,
            Message = "OTP sent successfully",
            RegistrationId = result.Value.RegistrationId,
            OtpLength = result.Value.OtpLength,
            OtpTtlSeconds = result.Value.OtpTtlSeconds,
        };
    }

    public override async Task<VerifyOTPResponse> VerifyOTP(VerifyOTPRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var command = new VerifyOtpCommand(
            RegistrationId: request.RegistrationId,
            Otp: request.Otp,
            PhoneNumber: request.PhoneNumber,
            TenantId: tenantId,
            DeviceId: null);

        var validation = await _verifyOtpValidator.ValidateAsync(command, context.CancellationToken);
        if (!validation.IsValid)
        {
            var errorMessage = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            throw new RpcException(new Status(StatusCode.InvalidArgument, errorMessage));
        }

        var result = await _verifyOtpHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(MapErrorToStatusCode(result.Error.Code), result.Error.Message));

        return new VerifyOTPResponse
        {
            Success = true,
            Message = "Phone verified. Please create your PIN.",
            AccountId = result.Value.AccountId,
            TemporaryToken = result.Value.TemporaryToken,
        };
    }

    // ── PIN Creation (STORY-010) ────────────────────────────────────

    public override async Task<CreatePINResponse> CreatePIN(CreatePINRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var command = new CreatePINCommand(
            AccountId: Guid.Parse(request.AccountId),
            Pin: request.Pin,
            PinConfirmation: request.PinConfirmation,
            TenantId: tenantId,
            DeviceId: null);

        return await _createPinHandler.HandleAsync(command, context.CancellationToken);
    }

    // ── Authentication (STORY-018) ───────────────────────────────────

    public override async Task<AuthenticateResponse> Authenticate(
        AuthenticateRequest request, ServerCallContext context)
    {
        var command = new AuthenticateCommand(
            PhoneNumber: request.PhoneNumber,
            Pin: request.Pin,
            DeviceId: request.DeviceId,
            TenantId: !string.IsNullOrEmpty(request.TenantId) ? request.TenantId : _tenantProvider.GetTenantId());

        var validation = await _authenticateValidator.ValidateAsync(command, context.CancellationToken);
        if (!validation.IsValid)
        {
            var errorMessage = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            throw new RpcException(new Status(StatusCode.InvalidArgument, errorMessage));
        }

        var result = await _authenticateHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
        {
            var statusCode = result.Error.Code switch
            {
                "Auth.Locked" => StatusCode.PermissionDenied,
                "Auth.DeviceMismatch" => StatusCode.FailedPrecondition,
                _ => StatusCode.Unauthenticated,
            };
            throw new RpcException(new Status(statusCode, result.Error.Message));
        }

        return new AuthenticateResponse
        {
            Success = true,
            AccessToken = result.Value.AccessToken,
            RefreshToken = result.Value.RefreshToken,
            AccessTokenExpiresIn = result.Value.AccessTokenExpiresIn,
            RefreshTokenExpiresIn = result.Value.RefreshTokenExpiresIn,
            AccountId = result.Value.AccountId,
            CustomerId = result.Value.CustomerId,
            Message = "Authenticated successfully."
        };
    }

    public override async Task<RefreshTokenResponse> RefreshToken(
        RefreshTokenRequest request, ServerCallContext context)
    {
        var command = new RefreshTokenCommand(
            RefreshToken: request.RefreshToken,
            DeviceId: request.DeviceId);

        var result = await _refreshTokenHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Unauthenticated, result.Error.Message));

        return new RefreshTokenResponse
        {
            Success = true,
            AccessToken = result.Value.AccessToken,
            RefreshToken = result.Value.RefreshToken,
            AccessTokenExpiresIn = result.Value.AccessTokenExpiresIn,
            RefreshTokenExpiresIn = result.Value.RefreshTokenExpiresIn,
            Message = "Token refreshed successfully."
        };
    }

    public override async Task<LogoutResponse> Logout(LogoutRequest request, ServerCallContext context)
    {
        var command = new LogoutCommand(
            AccountId: Guid.Parse(request.AccountId),
            AllDevices: request.AllDevices);

        var result = await _logoutHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        return new LogoutResponse
        {
            Success = true,
            Message = request.AllDevices ? "All sessions ended." : "Session ended."
        };
    }

    // ── Device Transfer (STORY-014) ──────────────────────────────────

    public override async Task<InitiateDeviceTransferResponse> InitiateDeviceTransfer(
        InitiateDeviceTransferRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var command = new InitiateDeviceTransferCommand(
            PhoneNumber: request.PhoneNumber,
            NewDeviceId: request.NewDeviceId,
            TenantId: tenantId);

        var validation = await _initiateTransferValidator.ValidateAsync(command, context.CancellationToken);
        if (!validation.IsValid)
        {
            var errorMessage = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            throw new RpcException(new Status(StatusCode.InvalidArgument, errorMessage));
        }

        var result = await _transferHandler.InitiateAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        return new InitiateDeviceTransferResponse
        {
            TransferReference = result.Value.TransferReference,
            Message = "Device transfer initiated. Check your phone for verification code.",
            OtpExpirySeconds = result.Value.OtpExpirySeconds
        };
    }

    public override async Task<CompleteDeviceTransferResponse> CompleteDeviceTransfer(
        CompleteDeviceTransferRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var command = new CompleteDeviceTransferCommand(
            TransferReference: request.TransferReference,
            Otp: request.Otp,
            Pin: request.Pin,
            NewDeviceId: request.NewDeviceId,
            TenantId: tenantId);

        var validation = await _completeTransferValidator.ValidateAsync(command, context.CancellationToken);
        if (!validation.IsValid)
        {
            var errorMessage = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            throw new RpcException(new Status(StatusCode.InvalidArgument, errorMessage));
        }

        var result = await _transferHandler.CompleteAsync(command, context.CancellationToken);

        if (result.IsFailure)
        {
            var statusCode = result.Error.Code switch
            {
                "Transfer.Expired" => StatusCode.DeadlineExceeded,
                "Auth.InvalidPIN" => StatusCode.Unauthenticated,
                _ => StatusCode.Internal,
            };
            throw new RpcException(new Status(statusCode, result.Error.Message));
        }

        return new CompleteDeviceTransferResponse
        {
            Success = true,
            AccessToken = result.Value.AccessToken,
            RefreshToken = result.Value.RefreshToken,
            Message = "Device transfer completed successfully."
        };
    }

    // ── Profile (STORY-015) ────────────────────────────────────────────

    public override async Task<ProfileResponse> GetProfile(
        GetProfileRequest request, ServerCallContext context)
    {
        var accountId = Guid.Parse(request.AccountId);
        var result = await _getProfileHandler.HandleAsync(accountId, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.NotFound, result.Error.Message));

        var p = result.Value;
        var response = new ProfileResponse
        {
            AccountId = p.AccountId,
            PhoneNumber = p.PhoneNumber,
            FirstName = p.FirstName ?? string.Empty,
            LastName = p.LastName ?? string.Empty,
            Email = p.Email ?? string.Empty,
            DateOfBirth = p.DateOfBirth ?? string.Empty,
            NationalId = p.NationalId ?? string.Empty,
            Status = MapAccountStatus(p.Status),
            KycLevel = p.KycLevel,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc)),
            LastLoginAt = p.LastLoginAt.HasValue
                ? Timestamp.FromDateTime(DateTime.SpecifyKind(p.LastLoginAt.Value, DateTimeKind.Utc))
                : null
        };

        foreach (var acct in p.Accounts)
        {
            response.Accounts.Add(new Protos.Accounts.AccountSummary
            {
                AccountId = acct.AccountId,
                Currency = acct.Currency,
                Balance = new Protos.Common.Money { Amount = acct.Balance.ToString("F2"), Currency = acct.Currency },
                AvailableBalance = new Protos.Common.Money { Amount = acct.AvailableBalance.ToString("F2"), Currency = acct.Currency },
                CardPanLast4 = acct.CardPanLast4 ?? string.Empty,
            });
        }

        return response;
    }

    public override async Task<ProfileResponse> UpdateProfile(
        UpdateProfileRequest request, ServerCallContext context)
    {
        var command = new UpdateProfileCommand(
            AccountId: Guid.Parse(request.AccountId),
            FirstName: request.FirstName,
            LastName: request.LastName,
            Email: request.Email,
            DateOfBirth: request.DateOfBirth,
            NationalId: request.NationalId);

        var result = await _updateProfileHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.NotFound, result.Error.Message));

        var p = result.Value;
        var updateResponse = new ProfileResponse
        {
            AccountId = p.AccountId,
            PhoneNumber = p.PhoneNumber,
            FirstName = p.FirstName ?? string.Empty,
            LastName = p.LastName ?? string.Empty,
            Email = p.Email ?? string.Empty,
            DateOfBirth = p.DateOfBirth ?? string.Empty,
            NationalId = p.NationalId ?? string.Empty,
            Status = MapAccountStatus(p.Status),
            KycLevel = p.KycLevel,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc)),
            LastLoginAt = p.LastLoginAt.HasValue
                ? Timestamp.FromDateTime(DateTime.SpecifyKind(p.LastLoginAt.Value, DateTimeKind.Utc))
                : null
        };

        foreach (var acct in p.Accounts)
        {
            updateResponse.Accounts.Add(new Protos.Accounts.AccountSummary
            {
                AccountId = acct.AccountId,
                Currency = acct.Currency,
                Balance = new Protos.Common.Money { Amount = acct.Balance.ToString("F2"), Currency = acct.Currency },
                AvailableBalance = new Protos.Common.Money { Amount = acct.AvailableBalance.ToString("F2"), Currency = acct.Currency },
                CardPanLast4 = acct.CardPanLast4 ?? string.Empty,
            });
        }

        return updateResponse;
    }

    // ── Balance (STORY-016) ──────────────────────────────────────────

    public override async Task<BalanceResponse> GetBalance(
        GetBalanceRequest request, ServerCallContext context)
    {
        var accountId = Guid.Parse(request.AccountId);
        var result = await _getBalanceHandler.HandleAsync(accountId, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.NotFound, result.Error.Message));

        var b = result.Value;
        return new BalanceResponse
        {
            AccountId = b.AccountId,
            Balance = new GoldBank.Protos.Common.Money { Amount = b.Balance.ToString("F2"), Currency = b.Currency },
            AvailableBalance = new GoldBank.Protos.Common.Money { Amount = b.AvailableBalance.ToString("F2"), Currency = b.Currency },
            DailyLimit = new GoldBank.Protos.Common.Money { Amount = b.DailyLimit.ToString("F2"), Currency = b.Currency }
        };
    }

    // ── Transactions (STORY-017) ─────────────────────────────────────

    public override async Task GetTransactions(
        GetTransactionsRequest request,
        IServerStreamWriter<TransactionResponse> responseStream,
        ServerCallContext context)
    {
        var query = new GetTransactionsQuery(
            AccountId: Guid.Parse(request.AccountId),
            StartDate: request.DateRange?.From?.ToDateTime(),
            EndDate: request.DateRange?.To?.ToDateTime(),
            TypeFilter: request.TypeFilter != TransactionType.Unspecified ? request.TypeFilter.ToString() : null,
            StatusFilter: request.StatusFilter != TransactionStatus.Unspecified ? request.StatusFilter.ToString() : null,
            Offset: ((request.Pagination?.Page ?? 1) - 1) * (request.Pagination?.PageSize ?? 50),
            Limit: request.Pagination?.PageSize ?? 50);

        var result = await _getTransactionsHandler.HandleAsync(query, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.NotFound, result.Error.Message));

        foreach (var tx in result.Value)
        {
            await responseStream.WriteAsync(new TransactionResponse
            {
                TransactionId = tx.TransactionId,
                Amount = new GoldBank.Protos.Common.Money { Amount = tx.Amount.ToString("F2"), Currency = tx.Currency },
                Fee = new GoldBank.Protos.Common.Money { Amount = tx.Fee.ToString("F2"), Currency = tx.Currency },
                Type = System.Enum.TryParse<TransactionType>(tx.Type, true, out var t) ? t : TransactionType.Unspecified,
                Status = System.Enum.TryParse<TransactionStatus>(tx.Status, true, out var s) ? s : TransactionStatus.Unspecified,
                Reference = tx.Reference ?? string.Empty,
                Description = tx.Description ?? string.Empty,
                CounterpartyName = tx.CounterpartyName ?? string.Empty,
                CounterpartyPhone = tx.CounterpartyPhone ?? string.Empty,
                BalanceAfter = new GoldBank.Protos.Common.Money { Amount = tx.BalanceAfter.ToString("F2"), Currency = tx.Currency },
                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(tx.CreatedAt, DateTimeKind.Utc)),
                CompletedAt = tx.CompletedAt.HasValue
                    ? Timestamp.FromDateTime(DateTime.SpecifyKind(tx.CompletedAt.Value, DateTimeKind.Utc))
                    : null
            }, context.CancellationToken);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static AccountStatus MapAccountStatus(string status) => status switch
    {
        "pending_kyc" => AccountStatus.PendingKyc,
        "active" => AccountStatus.Active,
        "suspended" => AccountStatus.Suspended,
        "closed" => AccountStatus.Closed,
        "frozen" => AccountStatus.Frozen,
        _ => AccountStatus.Unspecified,
    };

    private string ResolveTenantId(string requestTenantId)
    {
        if (!string.IsNullOrWhiteSpace(requestTenantId))
            return requestTenantId;

        return _tenantProvider.GetTenantId();
    }

    private static StatusCode MapErrorToStatusCode(string errorCode)
    {
        return errorCode switch
        {
            "Register.DuplicatePhone" => StatusCode.AlreadyExists,
            "Otp.RateLimitExceeded" => StatusCode.ResourceExhausted,
            "Otp.Expired" => StatusCode.DeadlineExceeded,
            "Otp.Invalid" => StatusCode.InvalidArgument,
            "Otp.Locked" => StatusCode.PermissionDenied,
            "Otp.PhoneMismatch" => StatusCode.InvalidArgument,
            "PhoneNumber.Required" => StatusCode.InvalidArgument,
            "PhoneNumber.InvalidFormat" => StatusCode.InvalidArgument,
            _ => StatusCode.Internal,
        };
    }
}
