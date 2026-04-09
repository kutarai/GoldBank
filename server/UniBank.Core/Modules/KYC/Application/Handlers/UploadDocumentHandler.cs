using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.KYC.Application.Commands;
using UniBank.Core.Modules.KYC.Domain.Entities;
using UniBank.Core.Modules.KYC.Infrastructure.Services;
using UniBank.SharedKernel.Events;
using UniBank.SharedKernel.Messaging;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.KYC.Application.Handlers;

/// <summary>
/// Handles document upload: encrypts, stores, and records KYC document (STORY-011).
/// </summary>
public sealed class UploadDocumentHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly DocumentEncryptionService _encryptionService;
    private readonly DocumentStorageService _storageService;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<UploadDocumentHandler> _logger;

    public UploadDocumentHandler(
        UniBankDbContext dbContext,
        DocumentEncryptionService encryptionService,
        DocumentStorageService storageService,
        IMessageBus messageBus,
        ILogger<UploadDocumentHandler> logger)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
        _storageService = storageService;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<Result<UploadDocumentResult>> HandleAsync(
        UploadDocumentCommand command, CancellationToken cancellationToken = default)
    {
        // Verify account exists
        var account = await _dbContext
            .Set<Modules.Accounts.Domain.Entities.Account>()
            .FirstOrDefaultAsync(a => a.Id == command.AccountId && a.DeletedAt == null, cancellationToken);

        if (account is null)
            return Result.Failure<UploadDocumentResult>(new Error("Account.NotFound", "Account not found."));

        // Encrypt document
        var checksum = _encryptionService.ComputeChecksum(command.FileData);
        var (encryptedData, keyRef) = _encryptionService.Encrypt(command.FileData);

        // Create document record
        var document = new KycDocument
        {
            AccountId = command.AccountId,
            DocumentType = command.DocumentType,
            FileName = command.FileName,
            ContentType = command.ContentType,
            FileSizeBytes = command.FileSize,
            EncryptionKeyRef = keyRef,
            ChecksumSha256 = checksum,
            Status = "uploaded",
            TenantId = command.TenantId,
            FileData = command.FileData, // raw bytes for direct admin display
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Store encrypted file
        var filePath = await _storageService.StoreAsync(
            command.TenantId, command.AccountId, document.Id,
            encryptedData, cancellationToken);

        document.FilePath = filePath;

        _dbContext.Set<KycDocument>().Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Publish domain event
        await _messageBus.PublishAsync(
            new KycDocumentUploaded(command.AccountId, document.Id, command.DocumentType)
            {
                TenantId = command.TenantId
            },
            cancellationToken);

        _logger.LogInformation(
            "KYC document uploaded: {DocumentId} type {DocumentType} for account {AccountId}",
            document.Id, command.DocumentType, command.AccountId);

        return Result.Success(new UploadDocumentResult(document.Id.ToString(), "uploaded"));
    }
}

public sealed record UploadDocumentResult(string DocumentId, string Status);
