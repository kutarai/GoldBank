using Microsoft.Extensions.Logging;

namespace GoldBank.Core.Modules.KYC.Infrastructure.Services;

/// <summary>
/// Stores encrypted KYC documents to the local filesystem (STORY-011).
/// In production, replace with cloud storage (S3, Azure Blob, etc.).
/// </summary>
public sealed class DocumentStorageService
{
    private readonly ILogger<DocumentStorageService> _logger;
    private const string BaseStoragePath = "data/kyc-documents";

    public DocumentStorageService(ILogger<DocumentStorageService> logger)
    {
        _logger = logger;
    }

    public async Task<string> StoreAsync(
        string tenantId, Guid accountId, Guid documentId,
        byte[] encryptedData, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(BaseStoragePath, tenantId, accountId.ToString());
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, $"{documentId}.enc");
        await File.WriteAllBytesAsync(filePath, encryptedData, cancellationToken);

        _logger.LogInformation(
            "Stored encrypted document {DocumentId} for account {AccountId}, size: {Size} bytes",
            documentId, accountId, encryptedData.Length);

        return filePath;
    }
}
