using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.AI.Application.Commands;
using UniBank.Core.Modules.AI.Domain.Entities;
using UniBank.Core.Modules.AI.Infrastructure.Services;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.AI.Application.Handlers;

public sealed record VerifyIdentityResult(
    string Decision,
    double FaceMatchScore,
    string FaceMatchConfidence,
    bool? NameMatch,
    bool? IdNumberMatch,
    bool? DobMatch,
    IdDocumentFields? ExtractedFields,
    string? RejectionReason);

public sealed class VerifyIdentityHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly FaceMatchingService _faceMatchingService;
    private readonly DocumentOcrService _documentOcrService;
    private readonly ILogger<VerifyIdentityHandler> _logger;

    public VerifyIdentityHandler(
        UniBankDbContext dbContext,
        FaceMatchingService faceMatchingService,
        DocumentOcrService documentOcrService,
        ILogger<VerifyIdentityHandler> logger)
    {
        _dbContext = dbContext;
        _faceMatchingService = faceMatchingService;
        _documentOcrService = documentOcrService;
        _logger = logger;
    }

    public async Task<Result<VerifyIdentityResult>> HandleAsync(
        VerifyIdentityCommand command, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == command.AccountId && a.DeletedAt == null, cancellationToken);

        if (account is null)
            return Result.Failure<VerifyIdentityResult>(
                new Error("Account.NotFound", "Account not found."));

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Run face matching and OCR in parallel
        var faceMatchTask = _faceMatchingService.CompareAsync(
            command.SelfieImage, command.IdDocumentImage, cancellationToken);
        var ocrTask = _documentOcrService.ExtractIdFieldsAsync(
            command.IdDocumentImage, cancellationToken);

        await Task.WhenAll(faceMatchTask, ocrTask);

        var faceResult = faceMatchTask.Result;
        var extractedFields = ocrTask.Result;

        // Compare extracted fields with account data
        DateTime? accountDob = DateTime.TryParse(account.DateOfBirth, out var parsedDob) ? parsedDob : null;
        var fieldComparisons = _documentOcrService.CompareIdFields(
            extractedFields,
            $"{account.FirstName} {account.LastName}",
            account.NationalId ?? "",
            accountDob);

        var nameMatch = fieldComparisons.FirstOrDefault(f => f.FieldName == "name")?.IsMatch;
        var idMatch = fieldComparisons.FirstOrDefault(f => f.FieldName == "id_number")?.IsMatch;
        var dobMatch = fieldComparisons.FirstOrDefault(f => f.FieldName == "date_of_birth")?.IsMatch;

        // Decision matrix
        var matchCount = new[] { nameMatch, idMatch, dobMatch }.Count(m => m == true);
        string overallDecision;
        string? rejectionReason = null;

        if (faceResult.Decision == "approved" && matchCount >= 2)
        {
            overallDecision = "auto_approved";
            account.Status = "active";
            account.KycLevel = Math.Max(account.KycLevel, 2);
        }
        else if (faceResult.Decision == "rejected" || (nameMatch == false && idMatch == false))
        {
            overallDecision = "rejected";
            rejectionReason = faceResult.Decision == "rejected"
                ? "Face match score too low."
                : "Name and ID number do not match the document.";
        }
        else
        {
            overallDecision = "manual_review";
        }

        sw.Stop();

        // Save KYC verification record
        var verification = new KycVerification
        {
            AccountId = command.AccountId,
            SelfieImagePath = $"kyc/{command.AccountId}/selfie_{DateTime.UtcNow:yyyyMMddHHmmss}.jpg",
            IdDocumentImagePath = $"kyc/{command.AccountId}/id_{DateTime.UtcNow:yyyyMMddHHmmss}.jpg",
            FaceMatchScore = faceResult.Score,
            FaceMatchDecision = faceResult.Decision,
            ExtractedFullName = extractedFields.FullName,
            ExtractedIdNumber = extractedFields.IdNumber,
            ExtractedDateOfBirth = extractedFields.DateOfBirth is not null
                ? DateTime.TryParse(extractedFields.DateOfBirth, out var dob) ? dob : null : null,
            ExtractedNationality = extractedFields.Nationality,
            ExtractedGender = extractedFields.Gender,
            NameMatch = nameMatch,
            IdNumberMatch = idMatch,
            DobMatch = dobMatch,
            OverallDecision = overallDecision,
            RejectionReason = rejectionReason,
            TenantId = command.TenantId,
        };

        _dbContext.KycVerifications.Add(verification);

        // Audit log
        _dbContext.AiInteractions.Add(new AiInteraction
        {
            AccountId = command.AccountId,
            InteractionType = "kyc_verify_identity",
            RequestSummary = $"KYC verification for account {command.AccountId}",
            ResponseSummary = $"Decision: {overallDecision}, Face: {faceResult.Score:F3}, Fields: {matchCount}/3",
            ModelUsed = "arcface+qwen3-vl",
            InferenceTimeMs = (int)sw.ElapsedMilliseconds,
            Success = true,
            TenantId = command.TenantId,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "KYC verification for {AccountId}: decision={Decision}, face={Score:F3}, fields={Matches}/3",
            command.AccountId, overallDecision, faceResult.Score, matchCount);

        return Result.Success(new VerifyIdentityResult(
            overallDecision, faceResult.Score, faceResult.Confidence,
            nameMatch, idMatch, dobMatch, extractedFields, rejectionReason));
    }
}
