using Grpc.Core;
using UniBank.Core.Modules.BillPay.Application.Commands;
using UniBank.Core.Modules.BillPay.Application.Handlers;
using UniBank.Protos.BillPay;
using UniBank.Protos.Common;
using UniBank.SharedKernel.MultiTenancy;
using GoogleTimestamp = Google.Protobuf.WellKnownTypes.Timestamp;

namespace UniBank.Core.Modules.BillPay.Grpc;

/// <summary>
/// gRPC service implementation for bill payment operations (STORY-037, 038, 039).
/// Maps proto-defined RPCs to application-layer handlers with proper validation and error handling.
/// </summary>
public sealed class BillPayGrpcService : BillPayService.BillPayServiceBase
{
    private readonly ListProvidersHandler _listProvidersHandler;
    private readonly PayBillHandler _payBillHandler;
    private readonly SaveBillerHandler _saveBillerHandler;
    private readonly GetSavedBillersHandler _getSavedBillersHandler;
    private readonly ITenantProvider _tenantProvider;

    public BillPayGrpcService(
        ListProvidersHandler listProvidersHandler,
        PayBillHandler payBillHandler,
        SaveBillerHandler saveBillerHandler,
        GetSavedBillersHandler getSavedBillersHandler,
        ITenantProvider tenantProvider)
    {
        _listProvidersHandler = listProvidersHandler;
        _payBillHandler = payBillHandler;
        _saveBillerHandler = saveBillerHandler;
        _getSavedBillersHandler = getSavedBillersHandler;
        _tenantProvider = tenantProvider;
    }

    // -- STORY-037: List Bill Providers ----------------------------------------

    public override async Task<ListProvidersResponse> ListProviders(
        ListProvidersRequest request, ServerCallContext context)
    {
        var category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category;
        var countryCode = string.IsNullOrWhiteSpace(request.CountryCode) ? null : request.CountryCode;

        var providers = await _listProvidersHandler.HandleAsync(
            category, countryCode, context.CancellationToken);

        var response = new ListProvidersResponse();

        foreach (var p in providers)
        {
            response.Providers.Add(new Protos.BillPay.BillProvider
            {
                ProviderId = p.Id.ToString(),
                Name = p.Name,
                Code = p.Code,
                Category = p.Category,
                RequiresMeterNumber = p.RequiresMeterNumber,
                RequiresAccountNumber = p.RequiresAccountNumber,
                MinAmount = new Money
                {
                    Amount = p.MinAmount.ToString("F2"),
                    Currency = p.Currency
                },
                MaxAmount = new Money
                {
                    Amount = p.MaxAmount.ToString("F2"),
                    Currency = p.Currency
                }
            });
        }

        return response;
    }

    // -- STORY-038: Pay Bill ---------------------------------------------------

    public override async Task<PayBillResponse> PayBill(
        PayBillRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();

        if (string.IsNullOrWhiteSpace(request.AccountId) || !Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        if (string.IsNullOrWhiteSpace(request.ProviderId) || !Guid.TryParse(request.ProviderId, out var providerId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid provider_id is required."));

        if (string.IsNullOrWhiteSpace(request.BillingReference))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "billing_reference is required."));

        if (request.Amount is null || !decimal.TryParse(request.Amount.Amount, out var amount) || amount <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid positive amount is required."));

        var currency = request.Amount.Currency;
        if (string.IsNullOrWhiteSpace(currency))
            currency = "ZWG";

        if (string.IsNullOrWhiteSpace(request.Pin))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "pin is required."));

        var command = new PayBillCommand(
            AccountId: accountId,
            ProviderId: providerId,
            BillingReference: request.BillingReference,
            Amount: amount,
            Currency: currency,
            Pin: request.Pin,
            TenantId: tenantId);

        var result = await _payBillHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(MapErrorToStatusCode(result.Error.Code), result.Error.Message));

        var value = result.Value;

        var response = new PayBillResponse
        {
            Success = true,
            Message = value.Token is not null
                ? $"Bill payment completed. Your token is {value.Token}."
                : "Bill payment completed successfully.",
            TransactionId = value.TransactionId,
            Reference = value.Reference,
            Token = value.Token ?? string.Empty,
            Amount = new Money
            {
                Amount = value.Amount.ToString("F2"),
                Currency = value.Currency
            },
            Fee = new Money
            {
                Amount = value.Fee.ToString("F2"),
                Currency = value.Currency
            },
            NewBalance = new Money
            {
                Amount = value.NewBalance.ToString("F2"),
                Currency = value.Currency
            },
            CompletedAt = GoogleTimestamp.FromDateTime(
                DateTime.SpecifyKind(value.CompletedAt, DateTimeKind.Utc))
        };

        return response;
    }

    // -- STORY-039: Save Biller ------------------------------------------------

    public override async Task<StatusResponse> SaveBiller(
        SaveBillerRequest request, ServerCallContext context)
    {
        var tenantId = _tenantProvider.GetTenantId();

        if (string.IsNullOrWhiteSpace(request.AccountId) || !Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        if (string.IsNullOrWhiteSpace(request.ProviderId) || !Guid.TryParse(request.ProviderId, out var providerId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid provider_id is required."));

        if (string.IsNullOrWhiteSpace(request.BillingReference))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "billing_reference is required."));

        if (string.IsNullOrWhiteSpace(request.Nickname))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "nickname is required."));

        var command = new SaveBillerCommand(
            AccountId: accountId,
            ProviderId: providerId,
            BillingReference: request.BillingReference,
            Nickname: request.Nickname,
            TenantId: tenantId);

        var result = await _saveBillerHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(MapErrorToStatusCode(result.Error.Code), result.Error.Message));

        return new StatusResponse
        {
            Success = true,
            Message = "Biller saved successfully."
        };
    }

    // -- STORY-039: Get Saved Billers ------------------------------------------

    public override async Task<GetSavedBillersResponse> GetSavedBillers(
        GetSavedBillersRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.AccountId) || !Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        var billers = await _getSavedBillersHandler.HandleAsync(accountId, context.CancellationToken);

        var response = new GetSavedBillersResponse();

        foreach (var b in billers)
        {
            var savedBiller = new Protos.BillPay.SavedBiller
            {
                Id = b.Id,
                ProviderId = b.ProviderId,
                ProviderName = b.ProviderName,
                BillingReference = b.BillingReference,
                Nickname = b.Nickname
            };

            if (b.LastPaidAt.HasValue)
            {
                savedBiller.LastPaidAt = GoogleTimestamp.FromDateTime(
                    DateTime.SpecifyKind(b.LastPaidAt.Value, DateTimeKind.Utc));
            }

            response.Billers.Add(savedBiller);
        }

        return response;
    }

    // -- Helpers ---------------------------------------------------------------

    private static StatusCode MapErrorToStatusCode(string errorCode)
    {
        return errorCode switch
        {
            "Account.NotFound" => StatusCode.NotFound,
            "Account.Inactive" => StatusCode.FailedPrecondition,
            "Account.NoPinSet" => StatusCode.FailedPrecondition,
            "Auth.InvalidPIN" => StatusCode.Unauthenticated,
            "BillPay.ProviderNotFound" => StatusCode.NotFound,
            "BillPay.ProviderInactive" => StatusCode.FailedPrecondition,
            "BillPay.InvalidReference" => StatusCode.InvalidArgument,
            "BillPay.InvalidNickname" => StatusCode.InvalidArgument,
            "BillPay.InvalidAmount" => StatusCode.InvalidArgument,
            "BillPay.BelowMinimum" => StatusCode.InvalidArgument,
            "BillPay.AboveMaximum" => StatusCode.InvalidArgument,
            "BillPay.PinRequired" => StatusCode.InvalidArgument,
            "BillPay.InsufficientFunds" => StatusCode.FailedPrecondition,
            "BillPay.DuplicateBiller" => StatusCode.AlreadyExists,
            _ => StatusCode.Internal,
        };
    }
}
