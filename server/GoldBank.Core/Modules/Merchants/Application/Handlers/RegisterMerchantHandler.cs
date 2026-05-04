using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Merchants.Application.Commands;
using GoldBank.Core.Modules.Merchants.Domain.Entities;
using GoldBank.Core.Modules.Merchants.Infrastructure.Services;
using GoldBank.SharedKernel.Events;
using GoldBank.SharedKernel.Messaging;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Merchants.Application.Handlers;

/// <summary>
/// Handles merchant registration (STORY-050).
/// Validates owner account, checks business name uniqueness, generates merchant code.
/// </summary>
public sealed class RegisterMerchantHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly MerchantIdGenerator _idGenerator;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<RegisterMerchantHandler> _logger;

    public RegisterMerchantHandler(
        GoldBankDbContext dbContext,
        MerchantIdGenerator idGenerator,
        IMessageBus messageBus,
        ILogger<RegisterMerchantHandler> logger)
    {
        _dbContext = dbContext;
        _idGenerator = idGenerator;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<Result<RegisterMerchantResult>> HandleAsync(
        RegisterMerchantCommand command, CancellationToken cancellationToken = default)
    {
        // Verify owner account exists and is active
        var ownerAccount = await _dbContext
            .Set<Modules.Accounts.Domain.Entities.Account>()
            .FirstOrDefaultAsync(
                a => a.Id == command.OwnerAccountId && a.DeletedAt == null,
                cancellationToken);

        if (ownerAccount is null)
            return Result.Failure<RegisterMerchantResult>(
                new Error("Merchant.OwnerNotFound", "Owner account not found."));

        if (ownerAccount.Status != "active")
            return Result.Failure<RegisterMerchantResult>(
                new Error("Merchant.OwnerNotActive", "Owner account must be active to register a merchant."));

        // Check for existing merchant for this owner
        var existingMerchant = await _dbContext.Set<Merchant>()
            .FirstOrDefaultAsync(
                m => m.OwnerAccountId == command.OwnerAccountId,
                cancellationToken);

        if (existingMerchant is not null)
            return Result.Failure<RegisterMerchantResult>(
                new Error("Merchant.AlreadyExists", "This account already has a merchant registration."));

        // Check business name uniqueness within tenant
        var duplicateName = await _dbContext.Set<Merchant>()
            .AnyAsync(
                m => m.BusinessName == command.BusinessName && m.TenantId == command.TenantId,
                cancellationToken);

        if (duplicateName)
            return Result.Failure<RegisterMerchantResult>(
                new Error("Merchant.DuplicateName", "A merchant with this business name already exists."));

        // Generate merchant code
        var tenantCode = command.TenantId.Length >= 2
            ? command.TenantId[..2]
            : command.TenantId;
        var merchantCode = await _idGenerator.GenerateAsync(tenantCode, cancellationToken);

        var merchant = new Merchant
        {
            MerchantCode = merchantCode,
            OwnerAccountId = command.OwnerAccountId,
            BusinessName = command.BusinessName,
            BusinessType = command.BusinessType,
            RegistrationNumber = command.RegistrationNumber,
            TaxId = command.TaxId,
            CategoryCode = command.CategoryCode,
            BusinessAddress = command.BusinessAddress,
            GpsLatitude = command.GpsLatitude.HasValue ? (decimal)command.GpsLatitude.Value : null,
            GpsLongitude = command.GpsLongitude.HasValue ? (decimal)command.GpsLongitude.Value : null,
            GpsAccuracyMeters = command.GpsAccuracyMeters.HasValue ? (decimal)command.GpsAccuracyMeters.Value : null,
            IsAgent = command.IsAgent,
            AgentTermsAccepted = command.AgentTermsAccepted,
            AgentTermsAcceptedAt = command.AgentTermsAccepted ? DateTime.UtcNow : null,
            Status = "pending_kyc",
            KycStatus = "pending",
            TenantId = command.TenantId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Set<Merchant>().Add(merchant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _messageBus.PublishAsync(
            new MerchantRegistered(
                merchant.Id, command.OwnerAccountId, merchantCode,
                command.BusinessName, command.IsAgent)
            {
                TenantId = command.TenantId
            },
            cancellationToken);

        _logger.LogInformation(
            "Merchant registered: {MerchantCode} for account {AccountId}",
            merchantCode, command.OwnerAccountId);

        return Result.Success(new RegisterMerchantResult(merchant.Id.ToString(), merchantCode, "pending_kyc"));
    }
}

public sealed record RegisterMerchantResult(string MerchantId, string MerchantCode, string Status);
