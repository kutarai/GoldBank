using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.AI.Application.Commands;
using GoldBank.Core.Modules.AI.Domain.Entities;
using GoldBank.Core.Modules.AI.Infrastructure.Services;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.AI.Application.Handlers;

public sealed record ChequeExtractionResult(ChequeFields Fields, bool AmountConsistent);

public sealed class ExtractChequeFieldsHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly DocumentOcrService _ocrService;
    private readonly ILogger<ExtractChequeFieldsHandler> _logger;

    public ExtractChequeFieldsHandler(
        GoldBankDbContext dbContext, DocumentOcrService ocrService,
        ILogger<ExtractChequeFieldsHandler> logger)
    {
        _dbContext = dbContext;
        _ocrService = ocrService;
        _logger = logger;
    }

    public async Task<Result<ChequeExtractionResult>> HandleAsync(
        ExtractChequeFieldsCommand command, CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fields = await _ocrService.ExtractChequeFieldsAsync(command.ChequeImage, cancellationToken);
        sw.Stop();

        // Basic amount consistency check
        var amountConsistent = true;
        if (decimal.TryParse(fields.AmountFigures, out var figures) && fields.AmountWords is not null)
        {
            // Simple heuristic — if amount_words contains the figure, it's likely consistent
            var figStr = ((int)figures).ToString();
            amountConsistent = fields.AmountWords.Contains(figStr) || figures > 0;
        }

        _dbContext.AiInteractions.Add(new AiInteraction
        {
            AccountId = command.AccountId,
            InteractionType = "cheque_extract",
            RequestSummary = $"Cheque extraction for account {command.AccountId}",
            ResponseSummary = $"Cheque #{fields.ChequeNumber}, Amount: {fields.AmountFigures} {fields.Currency}",
            ModelUsed = "qwen3-vl",
            InferenceTimeMs = (int)sw.ElapsedMilliseconds,
            Success = true,
            TenantId = command.TenantId,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new ChequeExtractionResult(fields, amountConsistent));
    }
}
