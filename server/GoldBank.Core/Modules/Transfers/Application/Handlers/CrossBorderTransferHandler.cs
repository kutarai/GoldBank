using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Accounts.Domain.Entities;
using GoldBank.Core.Modules.Transfers.Application.Commands;
using GoldBank.Core.Modules.Transfers.Domain.Entities;
using GoldBank.Core.Modules.Transfers.Domain.Events;
using GoldBank.Core.Modules.Transfers.Infrastructure.Services;
using GoldBank.SharedKernel.Events;
using GoldBank.SharedKernel.Messaging;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Transfers.Application.Handlers;

/// <summary>
/// Handles cross-border P2P transfers with currency conversion (STORY-030).
/// Verifies sender account, validates PIN, obtains exchange rate, calculates 2.5% fee,
/// debits sender in send currency, records outbound transaction with "processing" status,
/// and publishes events. Cross-border transfers have 1-3 business day estimated delivery.
/// </summary>
public sealed class CrossBorderTransferHandler
{
    private const decimal CrossBorderFeePercentage = 0.025m; // 2.5% fee for cross-border transfers
    private readonly GoldBankDbContext _dbContext;
    private readonly ExchangeRateService _exchangeRateService;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<CrossBorderTransferHandler> _logger;

    public CrossBorderTransferHandler(
        GoldBankDbContext dbContext,
        ExchangeRateService exchangeRateService,
        IMessageBus messageBus,
        ILogger<CrossBorderTransferHandler> logger)
    {
        _dbContext = dbContext;
        _exchangeRateService = exchangeRateService;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<Result<TransferResult>> HandleAsync(
        CrossBorderTransferCommand command, CancellationToken cancellationToken = default)
    {
        if (command.SendAmount <= 0)
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

        // Get exchange rate
        var conversionResult = _exchangeRateService.Convert(
            command.SendAmount, command.SendCurrency, command.ReceiveCurrency);

        if (conversionResult.IsFailure)
            return Result.Failure<TransferResult>(conversionResult.Error);

        var (receiveAmount, exchangeRate) = conversionResult.Value;

        // Calculate fee (2.5% of send amount)
        var fee = Math.Round(command.SendAmount * CrossBorderFeePercentage, 2);
        var totalDebit = command.SendAmount + fee;

        // Check sender balance
        if (senderAccount.Balance < totalDebit)
            return Result.Failure<TransferResult>(
                new Error("Transfer.InsufficientFunds",
                    $"Insufficient balance. Required: {totalDebit:F2}, Available: {senderAccount.Balance:F2}"));

        var reference = GenerateReference();
        var now = DateTime.UtcNow;
        var estimatedDelivery = CalculateEstimatedDelivery(now, command.RecipientCountry);

        // Debit sender
        senderAccount.Balance -= totalDebit;
        senderAccount.AvailableBalance -= totalDebit;
        senderAccount.UpdatedAt = now;

        // Create transfer record (cross-border starts as "processing")
        var transfer = new Transfer
        {
            SenderAccountId = command.SenderAccountId,
            RecipientPhone = command.RecipientPhone,
            RecipientName = command.RecipientName,
            Type = "cross_border",
            SendAmount = command.SendAmount,
            SendCurrency = command.SendCurrency,
            ReceiveAmount = receiveAmount,
            ReceiveCurrency = command.ReceiveCurrency,
            Fee = fee,
            ExchangeRate = exchangeRate.ToString("G"),
            Status = "processing",
            Reference = reference,
            EstimatedDelivery = estimatedDelivery,
            TenantId = command.TenantId
        };

        _dbContext.Set<Transfer>().Add(transfer);

        // Create sender transaction record (outbound)
        var senderTransaction = new Transaction
        {
            AccountId = senderAccount.Id,
            Type = "p2p_send",
            Amount = -totalDebit,
            Fee = fee,
            Status = "processing",
            Reference = reference,
            Description = $"Cross-border transfer to {command.RecipientName} ({command.RecipientCountry})",
            CounterpartyName = command.RecipientName,
            CounterpartyPhone = command.RecipientPhone,
            BalanceAfter = senderAccount.Balance,
            Currency = command.SendCurrency,
            TenantId = command.TenantId,
            CompletedAt = null
        };

        _dbContext.Transactions.Add(senderTransaction);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Publish TransactionCompleted event
        await _messageBus.PublishAsync(new TransactionCompleted(
            TransactionId: transfer.Id,
            SourceAccountId: senderAccount.Id,
            DestinationAccountId: null,
            Amount: command.SendAmount,
            Currency: command.SendCurrency,
            TransactionType: "cross_border_send",
            ReferenceNumber: reference), cancellationToken);

        // Publish transfer-specific event for notifications (STORY-031)
        await _messageBus.PublishAsync(new TransferCompletedEvent(
            TransferId: transfer.Id,
            SenderAccountId: senderAccount.Id,
            RecipientPhone: command.RecipientPhone,
            Amount: command.SendAmount,
            Currency: command.SendCurrency,
            Type: "cross_border")
        {
            TenantId = command.TenantId
        }, cancellationToken);

        _logger.LogInformation(
            "Cross-border transfer {Reference} initiated: {SendAmount} {SendCurrency} -> {ReceiveAmount} {ReceiveCurrency}, " +
            "sender {SenderId}, recipient {RecipientPhone} ({Country}), rate {Rate}, estimated delivery {Delivery}",
            reference, command.SendAmount, command.SendCurrency, receiveAmount, command.ReceiveCurrency,
            senderAccount.Id, command.RecipientPhone, command.RecipientCountry, exchangeRate, estimatedDelivery);

        return Result.Success(new TransferResult(
            TransactionId: transfer.Id.ToString(),
            Reference: reference,
            AmountSent: command.SendAmount,
            AmountReceived: receiveAmount,
            Fee: fee,
            Currency: command.SendCurrency,
            ReceiveCurrency: command.ReceiveCurrency,
            ExchangeRate: exchangeRate.ToString("G"),
            NewBalance: senderAccount.Balance,
            Status: "processing",
            EstimatedDelivery: estimatedDelivery));
    }

    /// <summary>
    /// Calculates estimated delivery based on corridor.
    /// Standard corridors (neighboring countries) get 1 business day.
    /// Other corridors get 3 business days.
    /// </summary>
    private static DateTime CalculateEstimatedDelivery(DateTime from, string recipientCountry)
    {
        // Neighboring / SADC countries get faster delivery
        var fastCorridorCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MW", "MZ", "ZW", "BW", "NA", "LS", "SZ", "TZ", "ZM", "KE", "NG"
        };

        var businessDays = fastCorridorCountries.Contains(recipientCountry) ? 1 : 3;
        return AddBusinessDays(from, businessDays);
    }

    private static DateTime AddBusinessDays(DateTime from, int days)
    {
        var result = from;
        var added = 0;
        while (added < days)
        {
            result = result.AddDays(1);
            if (result.DayOfWeek != DayOfWeek.Saturday && result.DayOfWeek != DayOfWeek.Sunday)
                added++;
        }
        return result;
    }

    private static string GenerateReference()
    {
        return $"XBR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..28].ToUpperInvariant();
    }
}
