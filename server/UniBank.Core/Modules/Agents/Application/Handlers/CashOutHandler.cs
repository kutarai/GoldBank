using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.Core.Modules.Agents.Application.Commands;
using UniBank.Core.Modules.Agents.Domain.Entities;
using UniBank.Core.Modules.Agents.Infrastructure.Services;
using UniBank.SharedKernel.Events;
using UniBank.SharedKernel.Messaging;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Agents.Application.Handlers;

/// <summary>
/// Handles cash-out at a merchant agent (STORY-033).
/// The customer withdraws cash from their mobile money account via the agent.
/// The customer's account is debited and the agent's float is credited.
/// </summary>
public sealed class CashOutHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly CommissionEngine _commissionEngine;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<CashOutHandler> _logger;

    public CashOutHandler(
        UniBankDbContext dbContext,
        CommissionEngine commissionEngine,
        IMessageBus messageBus,
        ILogger<CashOutHandler> logger)
    {
        _dbContext = dbContext;
        _commissionEngine = commissionEngine;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<Result<CashOperationResult>> HandleAsync(
        CashOutCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Amount <= 0)
            return Result.Failure<CashOperationResult>(
                new Error("Agent.InvalidAmount", "Cash-out amount must be greater than zero."));

        // Verify agent merchant exists, is active, and is an agent
        var merchant = await _dbContext.Merchants
            .FirstOrDefaultAsync(
                m => m.Id == command.AgentMerchantId && m.BusinessType == "agent",
                cancellationToken);

        if (merchant is null)
            return Result.Failure<CashOperationResult>(
                new Error("Agent.NotFound", "Agent merchant not found."));

        if (merchant.Status != "active")
            return Result.Failure<CashOperationResult>(
                new Error("Agent.Inactive", "Agent merchant is not active."));

        // Load the agent's owner account for PIN verification
        var agentAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == merchant.OwnerAccountId && a.DeletedAt == null,
                cancellationToken);

        if (agentAccount is null)
            return Result.Failure<CashOperationResult>(
                new Error("Agent.AccountNotFound", "Agent account not found."));

        // Verify agent PIN
        if (string.IsNullOrEmpty(agentAccount.PinHash))
            return Result.Failure<CashOperationResult>(
                new Error("Agent.NoPinSet", "Agent account does not have a PIN configured."));

        if (!BCrypt.Net.BCrypt.Verify(command.AgentPin, agentAccount.PinHash))
            return Result.Failure<CashOperationResult>(
                new Error("Agent.InvalidPin", "Invalid agent PIN."));

        // Verify customer account exists and is active
        var customerAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == command.CustomerAccountId && a.DeletedAt == null,
                cancellationToken);

        if (customerAccount is null)
            return Result.Failure<CashOperationResult>(
                new Error("Customer.NotFound", "Customer account not found."));

        if (customerAccount.Status != "active")
            return Result.Failure<CashOperationResult>(
                new Error("Customer.Inactive", "Customer account is not active."));

        // Verify customer PIN
        if (string.IsNullOrEmpty(customerAccount.PinHash))
            return Result.Failure<CashOperationResult>(
                new Error("Customer.NoPinSet", "Customer account does not have a PIN configured."));

        if (!BCrypt.Net.BCrypt.Verify(command.CustomerPin, customerAccount.PinHash))
            return Result.Failure<CashOperationResult>(
                new Error("Customer.InvalidPin", "Invalid customer PIN."));

        // Check customer balance
        if (customerAccount.AvailableBalance < command.Amount)
            return Result.Failure<CashOperationResult>(
                new Error("Customer.InsufficientFunds",
                    $"Insufficient balance. Available: {customerAccount.AvailableBalance:F2}, Required: {command.Amount:F2}"));

        // Load agent float
        var agentFloat = await _dbContext.AgentFloats
            .FirstOrDefaultAsync(
                f => f.MerchantId == command.AgentMerchantId && f.DeletedAt == null,
                cancellationToken);

        if (agentFloat is null)
            return Result.Failure<CashOperationResult>(
                new Error("Agent.NoFloat", "Agent float account not found."));

        // Calculate commission
        var (commissionRate, commissionAmount) = _commissionEngine.CalculateCommission("cash_out", command.Amount);

        var now = DateTime.UtcNow;
        var reference = $"CO-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        // Debit customer account
        customerAccount.Balance -= command.Amount;
        customerAccount.AvailableBalance -= command.Amount;
        customerAccount.UpdatedAt = now;

        // Credit agent float
        agentFloat.FloatBalance += command.Amount;
        agentFloat.UpdatedAt = now;

        // Create customer debit transaction
        var customerTransaction = new Transaction
        {
            AccountId = customerAccount.Id,
            Type = "cash_out",
            Amount = -command.Amount,
            Fee = 0m,
            Status = "completed",
            Reference = reference,
            Description = $"Cash-out at agent {merchant.BusinessName}",
            CounterpartyName = merchant.BusinessName,
            BalanceAfter = customerAccount.Balance,
            Currency = command.Currency,
            TenantId = command.TenantId,
            CompletedAt = now
        };

        // Create agent credit transaction
        var agentTransaction = new Transaction
        {
            AccountId = agentAccount.Id,
            Type = "cash_out",
            Amount = command.Amount,
            Fee = 0m,
            Status = "completed",
            Reference = reference,
            Description = $"Cash-out from {customerAccount.PhoneNumber}",
            CounterpartyName = $"{customerAccount.FirstName} {customerAccount.LastName}".Trim(),
            CounterpartyPhone = customerAccount.PhoneNumber,
            BalanceAfter = agentFloat.FloatBalance,
            Currency = command.Currency,
            TenantId = command.TenantId,
            CompletedAt = now
        };

        // Record commission
        var commission = new AgentCommission
        {
            MerchantId = command.AgentMerchantId,
            TransactionId = customerTransaction.Id,
            TransactionType = "cash_out",
            TransactionAmount = command.Amount,
            CommissionRate = commissionRate,
            CommissionAmount = commissionAmount,
            Currency = command.Currency,
            TenantId = command.TenantId
        };

        _dbContext.Transactions.Add(customerTransaction);
        _dbContext.Transactions.Add(agentTransaction);
        _dbContext.AgentCommissions.Add(commission);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Publish event
        await _messageBus.PublishAsync(new TransactionCompleted(
            TransactionId: customerTransaction.Id,
            SourceAccountId: customerAccount.Id,
            DestinationAccountId: agentAccount.Id,
            Amount: command.Amount,
            Currency: command.Currency,
            TransactionType: "cash_out",
            ReferenceNumber: reference), cancellationToken);

        _logger.LogInformation(
            "Cash-out completed: ref {Reference}, amount {Amount} {Currency}, agent {AgentId}, customer {CustomerId}",
            reference, command.Amount, command.Currency, command.AgentMerchantId, command.CustomerAccountId);

        return Result.Success(new CashOperationResult(
            TransactionId: customerTransaction.Id,
            Reference: reference,
            Amount: command.Amount,
            Commission: commissionAmount,
            NewFloatBalance: agentFloat.FloatBalance,
            Currency: command.Currency,
            CompletedAt: now));
    }
}
