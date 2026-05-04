namespace GoldBank.Core.Modules.Payments.Application.Commands;

/// <summary>
/// Command to generate an EMV QR code for merchant-presented payment (STORY-026).
/// The QR code follows EMV QRCPS (QR Code Payment Specification) merchant-presented mode.
/// </summary>
public sealed record GenerateQrCommand(
    string MerchantId,
    string? TerminalId,
    decimal Amount,
    string Currency,
    string Description,
    int TtlSeconds,
    string TenantId);
