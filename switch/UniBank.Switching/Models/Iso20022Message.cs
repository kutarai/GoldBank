using System.Xml.Linq;

namespace UniBank.Switching.Models;

/// <summary>
/// The supported ISO 20022 message types for the national payment switch.
/// </summary>
public enum Iso20022MessageType
{
    /// <summary>pacs.008 - FI to FI Customer Credit Transfer.</summary>
    Pacs008,

    /// <summary>pacs.002 - FI to FI Payment Status Report.</summary>
    Pacs002,

    /// <summary>pain.001 - Customer Credit Transfer Initiation.</summary>
    Pain001
}

/// <summary>
/// Represents an ISO 20022 XML financial message with a Business Application Header
/// and a document body. Supports the pacs.008, pacs.002, and pain.001 message types.
/// </summary>
public sealed class Iso20022Message
{
    /// <summary>
    /// The ISO 20022 message type.
    /// </summary>
    public Iso20022MessageType MessageType { get; set; }

    /// <summary>
    /// Unique message identification assigned by the sending institution.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Creation date/time of the message.
    /// </summary>
    public DateTime CreationDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// BIC or institution ID of the sending institution.
    /// </summary>
    public string SendingInstitution { get; set; } = string.Empty;

    /// <summary>
    /// BIC or institution ID of the receiving institution.
    /// </summary>
    public string ReceivingInstitution { get; set; } = string.Empty;

    /// <summary>
    /// End-to-end unique transaction identification.
    /// </summary>
    public string EndToEndId { get; set; } = string.Empty;

    /// <summary>
    /// Transaction identification assigned by the instructing agent.
    /// </summary>
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>
    /// Settlement amount as a decimal value.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// ISO 4217 currency code (e.g. "ZWG", "USD").
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// IBAN or account number of the debtor (sender).
    /// </summary>
    public string DebtorAccount { get; set; } = string.Empty;

    /// <summary>
    /// Name of the debtor.
    /// </summary>
    public string DebtorName { get; set; } = string.Empty;

    /// <summary>
    /// BIC of the debtor's agent (financial institution).
    /// </summary>
    public string DebtorAgent { get; set; } = string.Empty;

    /// <summary>
    /// IBAN or account number of the creditor (receiver).
    /// </summary>
    public string CreditorAccount { get; set; } = string.Empty;

    /// <summary>
    /// Name of the creditor.
    /// </summary>
    public string CreditorName { get; set; } = string.Empty;

    /// <summary>
    /// BIC of the creditor's agent (financial institution).
    /// </summary>
    public string CreditorAgent { get; set; } = string.Empty;

    /// <summary>
    /// Remittance information / payment reference.
    /// </summary>
    public string RemittanceInformation { get; set; } = string.Empty;

    /// <summary>
    /// Transaction status code (for pacs.002 status reports).
    /// </summary>
    public string StatusCode { get; set; } = string.Empty;

    /// <summary>
    /// Reason code for rejection or return (for pacs.002).
    /// </summary>
    public string ReasonCode { get; set; } = string.Empty;

    /// <summary>
    /// The raw XML document, if parsed from an inbound message.
    /// </summary>
    public XDocument? RawDocument { get; set; }
}
