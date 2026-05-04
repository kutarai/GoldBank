namespace GoldBank.Core.Modules.KYC.Application.Commands;

public sealed record UploadDocumentCommand(
    Guid AccountId,
    string DocumentType,
    string FileName,
    string ContentType,
    long FileSize,
    byte[] FileData,
    string TenantId);
