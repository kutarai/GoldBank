using Grpc.Core;
using Microsoft.Extensions.Logging;
using UniBank.Core.Modules.WhiteLabel.Application.Commands;
using UniBank.Core.Modules.WhiteLabel.Application.Handlers;
using UniBank.Core.Modules.WhiteLabel.Domain.Entities;
using UniBank.Protos.WhiteLabel;

namespace UniBank.Core.Modules.WhiteLabel.Grpc;

/// <summary>
/// gRPC service for white-label operations (STORY-068, STORY-070).
/// Handles tenant branding and fee/limit configuration.
/// </summary>
public sealed class WhiteLabelGrpcService : WhiteLabelService.WhiteLabelServiceBase
{
    private readonly GetBrandingHandler _getBrandingHandler;
    private readonly UpdateBrandingHandler _updateBrandingHandler;
    private readonly GetFeeConfigHandler _getFeeConfigHandler;
    private readonly UpdateFeeConfigHandler _updateFeeConfigHandler;
    private readonly ILogger<WhiteLabelGrpcService> _logger;

    public WhiteLabelGrpcService(
        GetBrandingHandler getBrandingHandler,
        UpdateBrandingHandler updateBrandingHandler,
        GetFeeConfigHandler getFeeConfigHandler,
        UpdateFeeConfigHandler updateFeeConfigHandler,
        ILogger<WhiteLabelGrpcService> logger)
    {
        _getBrandingHandler = getBrandingHandler;
        _updateBrandingHandler = updateBrandingHandler;
        _getFeeConfigHandler = getFeeConfigHandler;
        _updateFeeConfigHandler = updateFeeConfigHandler;
        _logger = logger;
    }

    /// <summary>
    /// Gets tenant branding configuration (STORY-068).
    /// </summary>
    public override async Task<BrandingResponse> GetBranding(
        GetBrandingRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Tenant ID is required."));

        _logger.LogDebug("GetBranding requested for tenant {TenantId}", request.TenantId);

        var result = await _getBrandingHandler.HandleAsync(request.TenantId, context.CancellationToken);

        if (result.IsFailure)
        {
            var statusCode = result.Error.Code switch
            {
                "Branding.NotFound" => StatusCode.NotFound,
                _ => StatusCode.Internal,
            };
            throw new RpcException(new Status(statusCode, result.Error.Message));
        }

        return MapBrandingResponse(result.Value);
    }

    /// <summary>
    /// Updates tenant branding configuration (STORY-068).
    /// </summary>
    public override async Task<BrandingResponse> UpdateBranding(
        UpdateBrandingRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Tenant ID is required."));

        var command = new UpdateBrandingCommand(
            TenantId: request.TenantId,
            AppName: request.AppName,
            LogoUrl: string.IsNullOrEmpty(request.LogoUrl) ? null : request.LogoUrl,
            PrimaryColor: request.PrimaryColor,
            SecondaryColor: request.SecondaryColor,
            AccentColor: request.AccentColor,
            FaviconUrl: string.IsNullOrEmpty(request.FaviconUrl) ? null : request.FaviconUrl,
            SupportEmail: string.IsNullOrEmpty(request.SupportEmail) ? null : request.SupportEmail,
            SupportPhone: string.IsNullOrEmpty(request.SupportPhone) ? null : request.SupportPhone,
            CustomCss: string.IsNullOrEmpty(request.CustomCss) ? null : request.CustomCss);

        var result = await _updateBrandingHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        return MapBrandingResponse(result.Value);
    }

    /// <summary>
    /// Gets per-tenant fee and limit configuration (STORY-070).
    /// </summary>
    public override async Task<FeeConfigResponse> GetFeeConfig(
        GetFeeConfigRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Tenant ID is required."));

        var result = await _getFeeConfigHandler.HandleAsync(request.TenantId, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        return MapFeeConfigResponse(result.Value);
    }

    /// <summary>
    /// Updates per-tenant fee and limit configuration (STORY-070).
    /// </summary>
    public override async Task<FeeConfigResponse> UpdateFeeConfig(
        UpdateFeeConfigRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Tenant ID is required."));

        var feeConfigs = request.FeeRules.Select(r => new TenantFeeConfig
        {
            TransactionType = r.TransactionType,
            FeeType = r.FeeType,
            Amount = ParseDecimal(r.Amount),
            Percentage = ParseDecimal(r.Percentage),
            MinFee = ParseDecimal(r.MinFee),
            MaxFee = ParseDecimal(r.MaxFee),
            Currency = string.IsNullOrEmpty(r.Currency) ? "ZWG" : r.Currency
        }).ToList();

        var limits = request.Limits.Select(l => new TenantTransactionLimit
        {
            TransactionType = l.TransactionType,
            PerTransactionLimit = ParseDecimal(l.PerTransactionLimit),
            DailyLimit = ParseDecimal(l.DailyLimit),
            MonthlyLimit = ParseDecimal(l.MonthlyLimit),
            Currency = string.IsNullOrEmpty(l.Currency) ? "ZWG" : l.Currency
        }).ToList();

        var result = await _updateFeeConfigHandler.HandleAsync(
            request.TenantId, feeConfigs, limits, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        return MapFeeConfigResponse(result.Value);
    }

    private static BrandingResponse MapBrandingResponse(TenantBranding branding) => new()
    {
        Success = true,
        TenantId = branding.TenantId,
        AppName = branding.AppName,
        LogoUrl = branding.LogoUrl ?? string.Empty,
        PrimaryColor = branding.PrimaryColor,
        SecondaryColor = branding.SecondaryColor,
        AccentColor = branding.AccentColor,
        FaviconUrl = branding.FaviconUrl ?? string.Empty,
        SupportEmail = branding.SupportEmail ?? string.Empty,
        SupportPhone = branding.SupportPhone ?? string.Empty,
        CustomCss = branding.CustomCss ?? string.Empty
    };

    private static FeeConfigResponse MapFeeConfigResponse(FeeConfigResult config)
    {
        var response = new FeeConfigResponse
        {
            Success = true,
            TenantId = config.TenantId
        };

        foreach (var fee in config.FeeConfigs)
        {
            response.FeeRules.Add(new FeeRule
            {
                TransactionType = fee.TransactionType,
                FeeType = fee.FeeType,
                Amount = fee.Amount.ToString("F4"),
                Percentage = fee.Percentage.ToString("F4"),
                MinFee = fee.MinFee.ToString("F4"),
                MaxFee = fee.MaxFee.ToString("F4"),
                Currency = fee.Currency
            });
        }

        foreach (var limit in config.TransactionLimits)
        {
            response.Limits.Add(new TransactionLimit
            {
                TransactionType = limit.TransactionType,
                PerTransactionLimit = limit.PerTransactionLimit.ToString("F4"),
                DailyLimit = limit.DailyLimit.ToString("F4"),
                MonthlyLimit = limit.MonthlyLimit.ToString("F4"),
                Currency = limit.Currency
            });
        }

        return response;
    }

    private static decimal ParseDecimal(string value)
    {
        return decimal.TryParse(value, out var result) ? result : 0m;
    }
}
