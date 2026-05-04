using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.AI.Application.Commands;
using GoldBank.Core.Modules.AI.Domain.Entities;
using GoldBank.Core.Modules.AI.Infrastructure.Services;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.AI.Application.Handlers;

public sealed record ProofOfAddressResult(
    string Decision, bool NameMatch, ProofOfAddressFields ExtractedFields,
    bool DocumentDateValid, int NewKycLevel);

public sealed class VerifyProofOfAddressHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly DocumentOcrService _ocrService;
    private readonly ILogger<VerifyProofOfAddressHandler> _logger;

    public VerifyProofOfAddressHandler(
        GoldBankDbContext dbContext, DocumentOcrService ocrService,
        ILogger<VerifyProofOfAddressHandler> logger)
    {
        _dbContext = dbContext;
        _ocrService = ocrService;
        _logger = logger;
    }

    public async Task<Result<ProofOfAddressResult>> HandleAsync(
        VerifyProofOfAddressCommand command, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == command.AccountId && a.DeletedAt == null, cancellationToken);

        if (account is null)
            return Result.Failure<ProofOfAddressResult>(
                new Error("Account.NotFound", "Account not found."));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fields = await _ocrService.ExtractProofOfAddressAsync(command.DocumentImage, cancellationToken);
        sw.Stop();

        // Name matching
        var accountName = $"{account.FirstName} {account.LastName}".ToLowerInvariant();
        var extractedName = fields.Name?.ToLowerInvariant() ?? "";
        var nameMatch = accountName.Split(' ').Count(p => extractedName.Contains(p)) >= 2;

        // Document date check (must be within 3 months)
        var documentDateValid = false;
        if (DateTime.TryParse(fields.DocumentDate, out var docDate))
            documentDateValid = (DateTime.UtcNow - docDate).TotalDays <= 90;

        var decision = nameMatch && documentDateValid ? "auto_approved" : "manual_review";
        var newKycLevel = account.KycLevel;

        if (decision == "auto_approved")
        {
            newKycLevel = Math.Max(account.KycLevel, 3);
            account.KycLevel = newKycLevel;
        }

        _dbContext.AiInteractions.Add(new AiInteraction
        {
            AccountId = command.AccountId,
            InteractionType = "proof_of_address",
            RequestSummary = $"Proof of address for account {command.AccountId}",
            ResponseSummary = $"Decision: {decision}, Name: {(nameMatch ? "match" : "mismatch")}, Date valid: {documentDateValid}",
            ModelUsed = "qwen3-vl",
            InferenceTimeMs = (int)sw.ElapsedMilliseconds,
            Success = true,
            TenantId = command.TenantId,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new ProofOfAddressResult(
            decision, nameMatch, fields, documentDateValid, newKycLevel));
    }
}
