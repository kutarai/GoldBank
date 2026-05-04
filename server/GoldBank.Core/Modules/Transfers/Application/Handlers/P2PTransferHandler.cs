using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Accounts.Domain.Entities;
using GoldBank.Core.Modules.Transfers.Application.Commands;
using GoldBank.Core.Modules.Transfers.Domain.Entities;
using GoldBank.Core.Modules.Transfers.Domain.Events;
using GoldBank.SharedKernel.Events;
using GoldBank.SharedKernel.Messaging;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Transfers.Application.Handlers;

/// <summary>
/// Handles domestic P2P transfers between accounts identified by phone number (STORY-029).
/// Verifies sender/recipient accounts, validates PIN, calculates 1% fee, debits sender,
/// credits recipient, creates transaction records for both parties, and publishes events.
/// </summary>
public sealed class P2PTransferHandler
{
    private const decimal DomesticFeePercentage = 0.01m; // 1% fee for domestic transfers
    private readonly GoldBankDbContext _dbContext;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<P2PTransferHandler> _logger;

    public P2PTransferHandler(
        GoldBankDbContext dbContext,
        IMessageBus messageBus,
        ILogger<P2PTransferHandler> logger)
    {
        _dbContext = dbContext;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<Result<TransferResult>> HandleAsync(
        P2PTransferCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Amount <= 0)
            return Result.Failure<TransferResult>(
                new Error("Transfer.InvalidAmount", "Transfer amount must be greater than zero."));

        // Verify sender account exists and is active
        var senderAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == command.SenderAccountId && a.DeletedAt == null,
                cancellationToken);

        if (senderAccount is null)
            return Result.Failure<TransferResult>(
                new Error("Account.NotFound", "Sender account not found."));

        if (senderAccount.Status != "active")
            return Result.Failure<TransferResult>(
                new Error("Account.Inactive", "Sender account is not active."));

        // Verify PIN
        if (string.IsNullOrEmpty(senderAccount.PinHash))
            return Result.Failure<TransferResult>(
                new Error("Account.NoPinSet", "Sender account does not have a PIN configured."));

        if (!BCrypt.Net.BCrypt.Verify(command.Pin, senderAccount.PinHash))
            return Result.Failure<TransferResult>(
                new Error("Auth.InvalidPIN", "Invalid PIN provided."));

        // Find recipient by phone number
        var recipientAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.PhoneNumber == command.RecipientPhone && a.DeletedAt == null,
                cancellationToken);

        if (recipientAccount is null)
            return Result.Failure<TransferResult>(
                new Error("Transfer.RecipientNotFound", "Recipient account not found for the provided phone number."));

        if (recipientAccount.Status != "active")
            return Result.Failure<TransferResult>(
                new Error("Transfer.RecipientInactive", "Recipient account is not active."));

        // Prevent self-transfer
        if (senderAccount.Id == recipientAccount.Id)
            return Result.Failure<TransferResult>(
                new Error("Transfer.SelfTransfer", "Cannot transfer to your own account."));

        // Calculate fee
        var fee = Math.Round(command.Amount * DomesticFeePercentage, 2);
        var totalDebit = command.Amount + fee;

        // Check sender balance
        if (senderAccount.Balance < totalDebit)
            return Result.Failure<TransferResult>(
                new Error("Transfer.InsufficientFunds",
                    $"Insufficient balance. Required: {totalDebit:F2}, Available: {senderAccount.Balance:F2}"));

        var reference = GenerateReference();
        var now = DateTime.UtcNow;

        // Debit sender
        senderAccount.Balance -= totalDebit;
        senderAccount.AvailableBalance -= totalDebit;
        senderAccount.UpdatedAt = now;

        // Credit recipient
        recipientAccount.Balance += command.Amount;
        recipientAccount.AvailableBalance += command.Amount;
        recipientAccount.UpdatedAt = now;

        // Create transfer record
        var transfer = new Transfer
        {
            SenderAccountId = command.SenderAccountId,
            RecipientAccountId = recipientAccount.Id,
            RecipientPhone = command.RecipientPhone,
            RecipientName = recipientAccount.FirstName is not null
                ? $"{recipientAccount.FirstName} {recipientAccount.LastName}".Trim()
                : recipientAccount.PhoneNumber,
            Type = "domestic",
            SendAmount = command.Amount,
            SendCurrency = command.Currency,
            ReceiveAmount = command.Amount,
            ReceiveCurrency = command.Currency,
            Fee = fee,
            Status = "completed",
            Reference = reference,
            Description = command.Description,
            CompletedAt = now,
            TenantId = command.TenantId
        };

        _dbContext.Set<Transfer>().Add(transfer);

        // Create transaction records for both parties
        var senderTransaction = new Transaction
        {
            AccountId = senderAccount.Id,
            Type = "p2p_send",
            Amount = -totalDebit,
            Fee = fee,
            Status = "completed",
            Reference = reference,
            Description = command.Description ?? $"Transfer to {transfer.RecipientName}",
            CounterpartyName = transfer.RecipientName,
            CounterpartyPhone = command.RecipientPhone,
            BalanceAfter = senderAccount.Balance,
            Currency = command.Currency,
            TenantId = command.TenantId,
            CompletedAt = now
        };

        var recipientTransaction = new Transaction
        {
            AccountId = recipientAccount.Id,
            Type = "p2p_receive",
            Amount = command.Amount,
            Fee = 0,
            Status = "completed",
            Reference = reference,
            Description = $"Transfer from {senderAccount.FirstName ?? senderAccount.PhoneNumber}",
            CounterpartyName = senderAccount.FirstName is not null
                ? $"{senderAccount.FirstName} {senderAccount.LastName}".Trim()
                : senderAccount.PhoneNumber,
            CounterpartyPhone = senderAccount.PhoneNumber,
            BalanceAfter = recipientAccount.Balance,
            Currency = command.Currency,
            TenantId = command.TenantId,
            CompletedAt = now
        };

        _dbContext.Transactions.Add(senderTransaction);
        _dbContext.Transactions.Add(recipientTransaction);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Publish TransactionCompleted event for both parties
        await _messageBus.PublishAsync(new TransactionCompleted(
            TransactionId: transfer.Id,
            SourceAccountId: senderAccount.Id,
            DestinationAccountId: recipientAccount.Id,
            Amount: command.Amount,
            Currency: command.Currency,
            TransactionType: "p2p_send",
            ReferenceNumber: reference), cancellationToken);

        // Publish transfer-specific event for notifications (STORY-031)
        await _messageBus.PublishAsync(new TransferCompletedEvent(
            TransferId: transfer.Id,
            SenderAccountId: senderAccount.Id,
            RecipientPhone: command.RecipientPhone,
            Amount: command.Amount,
            Currency: command.Currency,
            Type: "domestic")
        {
            TenantId = command.TenantId
        }, cancellationToken);

        _logger.LogInformation(
            "P2P transfer {Reference} completed: {Amount} {Currency}, sender {SenderId} -> recipient {RecipientId}",
            reference, command.Amount, command.Currency, senderAccount.Id, recipientAccount.Id);

        return Result.Success(new TransferResult(
            TransactionId: transfer.Id.ToString(),
            Reference: reference,
            AmountSent: command.Amount,
            AmountReceived: command.Amount,
            Fee: fee,
            Currency: command.Currency,
            ReceiveCurrency: command.Currency,
            ExchangeRate: null,
            NewBalance: senderAccount.Balance,
            Status: "completed",
            EstimatedDelivery: null));
    }

    private static string GenerateReference()
    {
        return $"TRF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..28].ToUpperInvariant();
    }
}
