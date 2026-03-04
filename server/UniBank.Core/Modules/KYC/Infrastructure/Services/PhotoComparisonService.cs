using Microsoft.Extensions.Logging;

namespace UniBank.Core.Modules.KYC.Infrastructure.Services;

/// <summary>
/// Compares a selfie against an ID document photo (STORY-012).
/// In production, this would call a third-party KYC/facial recognition API.
/// Current implementation provides a stub with configurable threshold.
/// </summary>
public sealed class PhotoComparisonService
{
    private const double AutoApproveThreshold = 0.85;
    private const double RejectThreshold = 0.40;
    private readonly ILogger<PhotoComparisonService> _logger;

    public PhotoComparisonService(ILogger<PhotoComparisonService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Compare selfie against ID document photo.
    /// Returns confidence score between 0.0 and 1.0.
    /// </summary>
    public Task<PhotoComparisonResult> CompareAsync(
        byte[] selfieData, byte[] idDocumentData, CancellationToken cancellationToken = default)
    {
        // Stub: In production, call a facial recognition API (e.g., AWS Rekognition, Azure Face API)
        // For now, return a high confidence score to allow flow testing
        var confidence = 0.92;

        var status = confidence switch
        {
            >= AutoApproveThreshold => "approved",
            <= RejectThreshold => "rejected",
            _ => "pending_review"
        };

        _logger.LogInformation(
            "Photo comparison result: confidence={Confidence}, status={Status}",
            confidence, status);

        return Task.FromResult(new PhotoComparisonResult(confidence, status));
    }
}

public sealed record PhotoComparisonResult(double Confidence, string Status);
