using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.AI.Application.Commands;
using GoldBank.Core.Modules.AI.Domain.Entities;
using GoldBank.Core.Modules.AI.Infrastructure.Services;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.AI.Application.Handlers;

public sealed record BillExtractionResult(BillFields Fields, string? MatchedProviderId);

public sealed class ExtractBillFieldsHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly DocumentOcrService _ocrService;
    private readonly ILogger<ExtractBillFieldsHandler> _logger;

    private static readonly Dictionary<string, string[]> KnownProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ZESA"] = ["zesa", "zetdc", "zimbabwe electricity"],
        ["TelOne"] = ["telone", "tel one"],
        ["NetOne"] = ["netone", "net one"],
        ["Econet"] = ["econet", "buddie"],
        ["Telecel"] = ["telecel"],
        ["CityOfHarare"] = ["city of harare", "harare city council", "harare municipality"],
        ["Nyaradzo"] = ["nyaradzo"],
        ["ZBC"] = ["zbc", "zimbabwe broadcasting"],
        ["ZINWA"] = ["zinwa", "zimbabwe national water"],
    };

    public ExtractBillFieldsHandler(
        GoldBankDbContext dbContext, DocumentOcrService ocrService,
        ILogger<ExtractBillFieldsHandler> logger)
    {
        _dbContext = dbContext;
        _ocrService = ocrService;
        _logger = logger;
    }

    public async Task<Result<BillExtractionResult>> HandleAsync(
        ExtractBillFieldsCommand command, CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fields = await _ocrService.ExtractBillFieldsAsync(command.BillImage, cancellationToken);
        sw.Stop();

        // Match provider
        string? matchedProviderId = null;
        if (fields.Provider is not null)
        {
            foreach (var (id, keywords) in KnownProviders)
            {
                if (keywords.Any(k => fields.Provider.Contains(k, StringComparison.OrdinalIgnoreCase)))
                {
                    matchedProviderId = id;
                    break;
                }
            }
        }

        _dbContext.AiInteractions.Add(new AiInteraction
        {
            AccountId = command.AccountId,
            InteractionType = "bill_extract",
            RequestSummary = $"Bill extraction for account {command.AccountId}",
            ResponseSummary = $"Provider: {fields.Provider} (matched: {matchedProviderId ?? "none"}), Amount: {fields.AmountDue}",
            ModelUsed = "qwen3-vl",
            InferenceTimeMs = (int)sw.ElapsedMilliseconds,
            Success = true,
            TenantId = command.TenantId,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new BillExtractionResult(fields, matchedProviderId));
    }
}
