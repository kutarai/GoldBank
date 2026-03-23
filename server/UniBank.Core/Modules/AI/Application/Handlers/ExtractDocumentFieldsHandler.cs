using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.AI.Application.Commands;
using UniBank.Core.Modules.AI.Domain.Entities;
using UniBank.Core.Modules.AI.Infrastructure.Services;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.AI.Application.Handlers;

public sealed record ExtractDocumentFieldsResult(
    IdDocumentFields? IdFields,
    ChequeFields? ChequeFields,
    BillFields? BillFields,
    ReceiptFields? ReceiptFields,
    PayslipFields? PayslipFields,
    ProofOfAddressFields? ProofOfAddressFields);

public sealed class ExtractDocumentFieldsHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly DocumentOcrService _ocrService;
    private readonly ILogger<ExtractDocumentFieldsHandler> _logger;

    public ExtractDocumentFieldsHandler(
        UniBankDbContext dbContext, DocumentOcrService ocrService,
        ILogger<ExtractDocumentFieldsHandler> logger)
    {
        _dbContext = dbContext;
        _ocrService = ocrService;
        _logger = logger;
    }

    public async Task<Result<ExtractDocumentFieldsResult>> HandleAsync(
        ExtractDocumentFieldsCommand command, CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ExtractDocumentFieldsResult result;

        try
        {
            if (command.DocumentType is not ("national_id" or "biometric_card" or "passport"
                or "cheque" or "bill" or "receipt" or "payslip" or "proof_of_address"))
            {
                return Result.Failure<ExtractDocumentFieldsResult>(
                    new Error("AI.UnknownDocumentType", $"Unknown document type: {command.DocumentType}"));
            }

            result = command.DocumentType switch
            {
                "national_id" or "biometric_card" or "passport" =>
                    new(await _ocrService.ExtractIdFieldsAsync(command.DocumentImage, cancellationToken),
                        null, null, null, null, null),
                "cheque" =>
                    new(null, await _ocrService.ExtractChequeFieldsAsync(command.DocumentImage, cancellationToken),
                        null, null, null, null),
                "bill" =>
                    new(null, null, await _ocrService.ExtractBillFieldsAsync(command.DocumentImage, cancellationToken),
                        null, null, null),
                "receipt" =>
                    new(null, null, null,
                        await _ocrService.ExtractReceiptFieldsAsync(command.DocumentImage, cancellationToken), null, null),
                "payslip" =>
                    new(null, null, null, null,
                        await _ocrService.ExtractPayslipFieldsAsync(command.DocumentImage, cancellationToken), null),
                _ => new(null, null, null, null, null,
                        await _ocrService.ExtractProofOfAddressAsync(command.DocumentImage, cancellationToken)),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document extraction failed for type {Type}", command.DocumentType);
            return Result.Failure<ExtractDocumentFieldsResult>(
                new Error("AI.ExtractionFailed", "Failed to extract document fields."));
        }

        sw.Stop();

        _dbContext.AiInteractions.Add(new AiInteraction
        {
            InteractionType = $"extract_{command.DocumentType}",
            RequestSummary = $"Extract fields from {command.DocumentType} document",
            ResponseSummary = "Extraction successful",
            ModelUsed = "qwen3-vl",
            InferenceTimeMs = (int)sw.ElapsedMilliseconds,
            Success = true,
            TenantId = command.TenantId,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(result);
    }
}
