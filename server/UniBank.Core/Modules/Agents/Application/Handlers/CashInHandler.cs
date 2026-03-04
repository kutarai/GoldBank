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
/// Handles cash-in at a merchant agent (STORY-032).
/// The agent receives physical cash and credits the customer's mobile money account.
/// Float is debited from the agent and credited to the customer.
/// </summary>
public sealed class CashInHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly CommissionEngine _commissionEngine;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<CashInHandler> _logger;

    public CashInHandler(
        UniBankDbContext dbContext,
        CommissionEngine commissionEngine,
        IMessageBus messageBus,
        ILogger<CashInHandler> logger)
    {
        _dbContext = dbContext;
        _commissionEngine = commissionEngine;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<Result<CashOperationResult>> HandleAsync(
        CashInCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Amount <= 0)
            return Result.Failure<CashOperationResult>(
                new Error("Agent.InvalidAmount", "Cash-in amount must be greater than zero."));

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

        // Find customer by phone number
        var customerAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.PhoneNumber == command.CustomerPhone && a.DeletedAt == null,
                cancellationToken);

        if (customerAccount is null)
            return Result.Failure<CashOperationResult>(
                new Error("Customer.NotFound", "Customer account not found for the given phone number."));

        if (customerAccount.Status != "active")
            return Result.Failure<CashOperationResult>(
                new Error("Customer.Inactive", "Customer account is not active."));

        // Check agent float balance
        var agentFloat = await _dbContext.AgentFloats
            .FirstOrDefaultAsync(
                f => f.MerchantId == command.AgentMerchantId && f.DeletedAt == null,
                cancellationToken);

        if (agentFloat is null)
            return Result.Failure<CashOperationResult>(
                new Error("Agent.NoFloat", "Agent float account not found."));

        if (agentFloat.FloatBalance < command.Amount)
            return Result.Failure<CashOperationResult>(
                new Error("Agent.InsufficientFloat",
                    $"Insufficient float balance. Available: {agentFloat.FloatBalance:F2}, Required: {command.Amount:F2}"));

        // Calculate commission
        var (commissionRate, commissionAmount) = _commissionEngine.CalculateCommission("cash_in", command.Amount);

        var now = DateTime.UtcNow;
        var reference = $"CI-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        // Debit agent float
        agentFloat.FloatBalance -= command.Amount;
        agentFloat.UpdatedAt = now;

        // Credit customer account
        customerAccount.Balance += command.Amount;
        customerAccount.AvailableBalance += command.Amount;
        customerAccount.UpdatedAt = now;

        // Create agent debit transaction
        var agentTransaction = new Transaction
        {
            AccountId = agentAccount.Id,
            Type = "cash_in",
            Amount = -command.Amount,
            Fee = 0m,
            Status = "completed",
            Reference = reference,
            Description = $"Cash-in to {command.CustomerPhone}",
            CounterpartyName = $"{customerAccount.FirstName} {customerAccount.LastName}".Trim(),
            CounterpartyPhone = command.CustomerPhone,
            BalanceAfter = agentFloat.FloatBalance,
            Currency = command.Currency,
            TenantId = command.TenantId,
            CompletedAt = now
        };

        // Create customer credit transaction
        var customerTransaction = new Transaction
        {
            AccountId = customerAccount.Id,
            Type = "cash_in",
            Amount = command.Amount,
            Fee = 0m,
            Status = "completed",
            Reference = reference,
            Description = $"Cash-in from agent {merchant.BusinessName}",
            CounterpartyName = merchant.BusinessName,
            BalanceAfter = customerAccount.Balance,
            Currency = command.Currency,
            TenantId = command.TenantId,
            CompletedAt = now
        };

        // Record commission
        var commission = new AgentCommission
        {
            MerchantId = command.AgentMerchantId,
            TransactionId = customerTransaction.Id,
            TransactionType = "cash_in",
            TransactionAmount = command.Amount,
            CommissionRate = commissionRate,
            CommissionAmount = commissionAmount,
            Currency = command.Currency,
            TenantId = command.TenantId
        };

        _dbContext.Transactions.Add(agentTransaction);
        _dbContext.Transactions.Add(customerTransaction);
        _dbContext.AgentCommissions.Add(commission);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Publish event
        await _messageBus.PublishAsync(new TransactionCompleted(
            TransactionId: customerTransaction.Id,
            SourceAccountId: agentAccount.Id,
            DestinationAccountId: customerAccount.Id,
            Amount: command.Amount,
            Currency: command.Currency,
            TransactionType: "cash_in",
            ReferenceNumber: reference), cancellationToken);

        _logger.LogInformation(
            "Cash-in completed: ref {Reference}, amount {Amount} {Currency}, agent {AgentId}, customer {CustomerPhone}",
            reference, command.Amount, command.Currency, command.AgentMerchantId, command.CustomerPhone);

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
