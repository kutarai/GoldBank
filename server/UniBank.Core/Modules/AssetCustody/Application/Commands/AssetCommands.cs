namespace UniBank.Core.Modules.AssetCustody.Application.Commands;

/// <summary>
/// Command to extract structured fields from a safe deposit receipt image (STORY-138).
/// </summary>
public sealed record ExtractDepositReceiptCommand(
    Guid AccountId,
    byte[] ReceiptImage,
    string TenantId);
