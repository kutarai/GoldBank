namespace UniBank.Switching.Models;

/// <summary>
/// Categorises the type of payment message flowing through the switch.
/// </summary>
public enum CanonicalMessageType
{
    /// <summary>Financial request (purchase, withdrawal, transfer).</summary>
    FinancialRequest,

    /// <summary>Financial response (approval, decline).</summary>
    FinancialResponse,

    /// <summary>Authorization request.</summary>
    AuthorizationRequest,

    /// <summary>Authorization response.</summary>
    AuthorizationResponse,

    /// <summary>Reversal request.</summary>
    ReversalRequest,

    /// <summary>Reversal response.</summary>
    ReversalResponse,

    /// <summary>Payment status report.</summary>
    StatusReport
}

/// <summary>
/// Internal canonical message format used by the switching engine. All inbound ISO 8583
/// and ISO 20022 messages are translated to this format before routing, and translated
/// back to the destination institution's preferred protocol on the outbound path.
/// </summary>
public sealed class CanonicalMessage
{
    /// <summary>
    /// Unique identifier for this transaction within the switch.
    /// </summary>
    public string TransactionId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The type of message (request, response, reversal, status report).
    /// </summary>
    public CanonicalMessageType MessageType { get; set; }

    /// <summary>
    /// Code or BIC of the originating financial institution.
    /// </summary>
    public string SourceInstitution { get; set; } = string.Empty;

    /// <summary>
    /// Code or BIC of the destination financial institution.
    /// </summary>
    public string DestinationInstitution { get; set; } = string.Empty;

    /// <summary>
    /// Transaction amount in minor units (the decimal value).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// ISO 4217 currency code (e.g. "ZWG").
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Account to debit (sender's account).
    /// </summary>
    public string DebitAccount { get; set; } = string.Empty;

    /// <summary>
    /// Account to credit (receiver's account).
    /// </summary>
    public string CreditAccount { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable or system-generated payment reference.
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the message entered the switch.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Processing code from the original ISO 8583 message (e.g. "000000" for purchase).
    /// </summary>
    public string ProcessingCode { get; set; } = string.Empty;

    /// <summary>
    /// Response code from the destination (e.g. "00" for approved).
    /// </summary>
    public string ResponseCode { get; set; } = string.Empty;

    /// <summary>
    /// Authorization code assigned by the approving institution.
    /// </summary>
    public string AuthorizationCode { get; set; } = string.Empty;

    /// <summary>
    /// System Trace Audit Number for ISO 8583 tracing.
    /// </summary>
    public string Stan { get; set; } = string.Empty;

    /// <summary>
    /// Retrieval Reference Number for reconciliation.
    /// </summary>
    public string RetrievalReference { get; set; } = string.Empty;

    /// <summary>
    /// Terminal or channel identifier.
    /// </summary>
    public string TerminalId { get; set; } = string.Empty;

    /// <summary>
    /// Merchant identifier.
    /// </summary>
    public string MerchantId { get; set; } = string.Empty;

    /// <summary>
    /// Merchant name and location.
    /// </summary>
    public string MerchantName { get; set; } = string.Empty;

    /// <summary>
    /// Free-form additional data as key/value pairs for protocol-specific fields
    /// that don't map to a dedicated property.
    /// </summary>
    public Dictionary<string, string> AdditionalData { get; set; } = new();
}
