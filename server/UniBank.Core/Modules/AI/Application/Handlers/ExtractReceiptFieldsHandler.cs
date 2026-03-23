using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.AI.Application.Commands;
using UniBank.Core.Modules.AI.Domain.Entities;
using UniBank.Core.Modules.AI.Infrastructure.Services;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.AI.Application.Handlers;

public sealed record ReceiptExtractionResult(ReceiptFields Fields, bool TransactionMatched);

public sealed class ExtractReceiptFieldsHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly DocumentOcrService _ocrService;
    private readonly ILogger<ExtractReceiptFieldsHandler> _logger;

    public ExtractReceiptFieldsHandler(
        UniBankDbContext dbContext, DocumentOcrService ocrService,
        ILogger<ExtractReceiptFieldsHandler> logger)
    {
        _dbContext = dbContext;
        _ocrService = ocrService;
        _logger = logger;
    }

    public async Task<Result<ReceiptExtractionResult>> HandleAsync(
        ExtractReceiptFieldsCommand command, CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fields = await _ocrService.ExtractReceiptFieldsAsync(command.ReceiptImage, cancellationToken);
        sw.Stop();

        var transactionMatched = false;
        if (command.TransactionId != Guid.Empty && decimal.TryParse(fields.TotalAmount, out var receiptAmount))
        {
            var txn = await _dbContext.Transactions.FindAsync([command.TransactionId], cancellationToken);
            if (txn is not null && Math.Abs(txn.Amount) == receiptAmount)
                transactionMatched = true;
        }

        _dbContext.AiInteractions.Add(new AiInteraction
        {
            AccountId = command.AccountId,
            InteractionType = "receipt_extract",
            RequestSummary = $"Receipt extraction for account {command.AccountId}",
            ResponseSummary = $"Merchant: {fields.MerchantName}, Amount: {fields.TotalAmount}, Category: {fields.Category}",
            ModelUsed = "qwen3-vl",
            InferenceTimeMs = (int)sw.ElapsedMilliseconds,
            Success = true,
            TenantId = command.TenantId,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new ReceiptExtractionResult(fields, transactionMatched));
    }
}
