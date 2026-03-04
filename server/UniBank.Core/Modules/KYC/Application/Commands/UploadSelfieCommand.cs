namespace UniBank.Core.Modules.KYC.Application.Commands;

public sealed record UploadSelfieCommand(
    Guid AccountId,
    string ContentType,
    long FileSize,
    byte[] FileData,
    string? LivenessToken,
    string TenantId);
