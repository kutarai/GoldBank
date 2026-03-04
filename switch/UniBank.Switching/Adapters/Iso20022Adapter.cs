using System.Globalization;
using System.Xml.Linq;
using UniBank.SharedKernel.Results;
using UniBank.Switching.Models;

namespace UniBank.Switching.Adapters;

/// <summary>
/// Parses and generates ISO 20022 XML messages for the national payment switch.
/// Supports pacs.008 (FI-to-FI Customer Credit Transfer), pacs.002 (Payment Status Report),
/// and pain.001 (Customer Credit Transfer Initiation).
/// </summary>
public sealed class Iso20022Adapter
{
    private static readonly XNamespace Pacs008Ns =
        "urn:iso:std:iso:20022:tech:xsd:pacs.008.001.10";

    private static readonly XNamespace Pacs002Ns =
        "urn:iso:std:iso:20022:tech:xsd:pacs.002.001.12";

    private static readonly XNamespace Pain001Ns =
        "urn:iso:std:iso:20022:tech:xsd:pain.001.001.11";

    /// <summary>
    /// Parses an ISO 20022 XML string into an <see cref="Iso20022Message"/>.
    /// Auto-detects the message type from the root element namespace.
    /// </summary>
    public Result<Iso20022Message> Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return Result.Failure<Iso20022Message>(
                new Error("ISO20022.EmptyXml", "XML content is null or empty."));
        }

        try
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root is null)
            {
                return Result.Failure<Iso20022Message>(
                    new Error("ISO20022.NoRoot", "XML document has no root element."));
            }

            var ns = root.Name.Namespace;

            if (ns == Pacs008Ns || RootContains(root, "FIToFICstmrCdtTrf"))
            {
                return ParsePacs008(doc);
            }

            if (ns == Pacs002Ns || RootContains(root, "FIToFIPmtStsRpt"))
            {
                return ParsePacs002(doc);
            }

            if (ns == Pain001Ns || RootContains(root, "CstmrCdtTrfInitn"))
            {
                return ParsePain001(doc);
            }

            return Result.Failure<Iso20022Message>(
                new Error("ISO20022.UnknownType",
                    $"Unable to determine ISO 20022 message type from root element '{root.Name}'."));
        }
        catch (Exception ex)
        {
            return Result.Failure<Iso20022Message>(
                new Error("ISO20022.ParseError", $"Failed to parse ISO 20022 XML: {ex.Message}"));
        }
    }

    /// <summary>
    /// Generates an ISO 20022 XML string from an <see cref="Iso20022Message"/>.
    /// </summary>
    public Result<string> Generate(Iso20022Message message)
    {
        if (message is null)
        {
            return Result.Failure<string>(
                new Error("ISO20022.NullMessage", "Message cannot be null."));
        }

        try
        {
            var doc = message.MessageType switch
            {
                Iso20022MessageType.Pacs008 => GeneratePacs008(message),
                Iso20022MessageType.Pacs002 => GeneratePacs002(message),
                Iso20022MessageType.Pain001 => GeneratePain001(message),
                _ => throw new ArgumentException($"Unsupported message type: {message.MessageType}")
            };

            return Result.Success(doc.ToString(SaveOptions.None));
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(
                new Error("ISO20022.GenerateError", $"Failed to generate ISO 20022 XML: {ex.Message}"));
        }
    }

    #region pacs.008 - FI to FI Customer Credit Transfer

    private static Result<Iso20022Message> ParsePacs008(XDocument doc)
    {
        var message = new Iso20022Message
        {
            MessageType = Iso20022MessageType.Pacs008,
            RawDocument = doc
        };

        var ns = FindNamespace(doc, "FIToFICstmrCdtTrf") ?? Pacs008Ns;

        var grpHdr = doc.Descendants(ns + "GrpHdr").FirstOrDefault();
        if (grpHdr is not null)
        {
            message.MessageId = ElementValue(grpHdr, ns, "MsgId");
            message.CreationDateTime = ParseDateTime(ElementValue(grpHdr, ns, "CreDtTm"));
            message.SendingInstitution = DeepElementValue(grpHdr, ns, "InstgAgt", "FinInstnId", "BIC")
                                         ?? DeepElementValue(grpHdr, ns, "InstgAgt", "FinInstnId", "BICFI")
                                         ?? string.Empty;
            message.ReceivingInstitution = DeepElementValue(grpHdr, ns, "InstdAgt", "FinInstnId", "BIC")
                                           ?? DeepElementValue(grpHdr, ns, "InstdAgt", "FinInstnId", "BICFI")
                                           ?? string.Empty;
        }

        var cdtTrf = doc.Descendants(ns + "CdtTrfTxInf").FirstOrDefault();
        if (cdtTrf is not null)
        {
            message.EndToEndId = DeepElementValue(cdtTrf, ns, "PmtId", "EndToEndId") ?? string.Empty;
            message.TransactionId = DeepElementValue(cdtTrf, ns, "PmtId", "TxId") ?? string.Empty;

            var amtEl = cdtTrf.Descendants(ns + "InstdAmt").FirstOrDefault()
                        ?? cdtTrf.Descendants(ns + "IntrBkSttlmAmt").FirstOrDefault();
            if (amtEl is not null)
            {
                message.Amount = ParseDecimal(amtEl.Value);
                message.Currency = amtEl.Attribute("Ccy")?.Value ?? string.Empty;
            }

            message.DebtorName = DeepElementValue(cdtTrf, ns, "Dbtr", "Nm") ?? string.Empty;
            message.DebtorAccount = DeepElementValue(cdtTrf, ns, "DbtrAcct", "Id", "IBAN")
                                    ?? DeepElementValue(cdtTrf, ns, "DbtrAcct", "Id", "Othr", "Id")
                                    ?? string.Empty;
            message.DebtorAgent = DeepElementValue(cdtTrf, ns, "DbtrAgt", "FinInstnId", "BIC")
                                  ?? DeepElementValue(cdtTrf, ns, "DbtrAgt", "FinInstnId", "BICFI")
                                  ?? string.Empty;

            message.CreditorName = DeepElementValue(cdtTrf, ns, "Cdtr", "Nm") ?? string.Empty;
            message.CreditorAccount = DeepElementValue(cdtTrf, ns, "CdtrAcct", "Id", "IBAN")
                                      ?? DeepElementValue(cdtTrf, ns, "CdtrAcct", "Id", "Othr", "Id")
                                      ?? string.Empty;
            message.CreditorAgent = DeepElementValue(cdtTrf, ns, "CdtrAgt", "FinInstnId", "BIC")
                                    ?? DeepElementValue(cdtTrf, ns, "CdtrAgt", "FinInstnId", "BICFI")
                                    ?? string.Empty;

            message.RemittanceInformation = DeepElementValue(cdtTrf, ns, "RmtInf", "Ustrd") ?? string.Empty;
        }

        return Result.Success(message);
    }

    private static XDocument GeneratePacs008(Iso20022Message message)
    {
        var ns = Pacs008Ns;
        var creationDate = message.CreationDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "Document",
                new XElement(ns + "FIToFICstmrCdtTrf",
                    new XElement(ns + "GrpHdr",
                        new XElement(ns + "MsgId", message.MessageId),
                        new XElement(ns + "CreDtTm", creationDate),
                        new XElement(ns + "NbOfTxs", "1"),
                        new XElement(ns + "SttlmInf",
                            new XElement(ns + "SttlmMtd", "CLRG")),
                        new XElement(ns + "InstgAgt",
                            new XElement(ns + "FinInstnId",
                                new XElement(ns + "BICFI", message.SendingInstitution))),
                        new XElement(ns + "InstdAgt",
                            new XElement(ns + "FinInstnId",
                                new XElement(ns + "BICFI", message.ReceivingInstitution)))),
                    new XElement(ns + "CdtTrfTxInf",
                        new XElement(ns + "PmtId",
                            new XElement(ns + "EndToEndId", message.EndToEndId),
                            new XElement(ns + "TxId", message.TransactionId)),
                        new XElement(ns + "IntrBkSttlmAmt",
                            new XAttribute("Ccy", message.Currency),
                            message.Amount.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement(ns + "Dbtr",
                            new XElement(ns + "Nm", message.DebtorName)),
                        new XElement(ns + "DbtrAcct",
                            new XElement(ns + "Id",
                                new XElement(ns + "Othr",
                                    new XElement(ns + "Id", message.DebtorAccount)))),
                        new XElement(ns + "DbtrAgt",
                            new XElement(ns + "FinInstnId",
                                new XElement(ns + "BICFI", message.DebtorAgent))),
                        new XElement(ns + "Cdtr",
                            new XElement(ns + "Nm", message.CreditorName)),
                        new XElement(ns + "CdtrAcct",
                            new XElement(ns + "Id",
                                new XElement(ns + "Othr",
                                    new XElement(ns + "Id", message.CreditorAccount)))),
                        new XElement(ns + "CdtrAgt",
                            new XElement(ns + "FinInstnId",
                                new XElement(ns + "BICFI", message.CreditorAgent))),
                        new XElement(ns + "RmtInf",
                            new XElement(ns + "Ustrd", message.RemittanceInformation))))));
    }

    #endregion

    #region pacs.002 - Payment Status Report

    private static Result<Iso20022Message> ParsePacs002(XDocument doc)
    {
        var message = new Iso20022Message
        {
            MessageType = Iso20022MessageType.Pacs002,
            RawDocument = doc
        };

        var ns = FindNamespace(doc, "FIToFIPmtStsRpt") ?? Pacs002Ns;

        var grpHdr = doc.Descendants(ns + "GrpHdr").FirstOrDefault();
        if (grpHdr is not null)
        {
            message.MessageId = ElementValue(grpHdr, ns, "MsgId");
            message.CreationDateTime = ParseDateTime(ElementValue(grpHdr, ns, "CreDtTm"));
            message.SendingInstitution = DeepElementValue(grpHdr, ns, "InstgAgt", "FinInstnId", "BIC")
                                         ?? DeepElementValue(grpHdr, ns, "InstgAgt", "FinInstnId", "BICFI")
                                         ?? string.Empty;
            message.ReceivingInstitution = DeepElementValue(grpHdr, ns, "InstdAgt", "FinInstnId", "BIC")
                                           ?? DeepElementValue(grpHdr, ns, "InstdAgt", "FinInstnId", "BICFI")
                                           ?? string.Empty;
        }

        var txInfAndSts = doc.Descendants(ns + "TxInfAndSts").FirstOrDefault();
        if (txInfAndSts is not null)
        {
            message.TransactionId = ElementValue(txInfAndSts, ns, "OrgnlTxId");
            message.EndToEndId = ElementValue(txInfAndSts, ns, "OrgnlEndToEndId");
            message.StatusCode = ElementValue(txInfAndSts, ns, "TxSts");
            message.ReasonCode = DeepElementValue(txInfAndSts, ns, "StsRsnInf", "Rsn", "Cd") ?? string.Empty;
        }

        return Result.Success(message);
    }

    private static XDocument GeneratePacs002(Iso20022Message message)
    {
        var ns = Pacs002Ns;
        var creationDate = message.CreationDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

        var txInfAndSts = new XElement(ns + "TxInfAndSts",
            new XElement(ns + "OrgnlEndToEndId", message.EndToEndId),
            new XElement(ns + "OrgnlTxId", message.TransactionId),
            new XElement(ns + "TxSts", message.StatusCode));

        if (!string.IsNullOrEmpty(message.ReasonCode))
        {
            txInfAndSts.Add(
                new XElement(ns + "StsRsnInf",
                    new XElement(ns + "Rsn",
                        new XElement(ns + "Cd", message.ReasonCode))));
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "Document",
                new XElement(ns + "FIToFIPmtStsRpt",
                    new XElement(ns + "GrpHdr",
                        new XElement(ns + "MsgId", message.MessageId),
                        new XElement(ns + "CreDtTm", creationDate),
                        new XElement(ns + "InstgAgt",
                            new XElement(ns + "FinInstnId",
                                new XElement(ns + "BICFI", message.SendingInstitution))),
                        new XElement(ns + "InstdAgt",
                            new XElement(ns + "FinInstnId",
                                new XElement(ns + "BICFI", message.ReceivingInstitution)))),
                    txInfAndSts)));
    }

    #endregion

    #region pain.001 - Customer Credit Transfer Initiation

    private static Result<Iso20022Message> ParsePain001(XDocument doc)
    {
        var message = new Iso20022Message
        {
            MessageType = Iso20022MessageType.Pain001,
            RawDocument = doc
        };

        var ns = FindNamespace(doc, "CstmrCdtTrfInitn") ?? Pain001Ns;

        var grpHdr = doc.Descendants(ns + "GrpHdr").FirstOrDefault();
        if (grpHdr is not null)
        {
            message.MessageId = ElementValue(grpHdr, ns, "MsgId");
            message.CreationDateTime = ParseDateTime(ElementValue(grpHdr, ns, "CreDtTm"));
        }

        var pmtInf = doc.Descendants(ns + "PmtInf").FirstOrDefault();
        if (pmtInf is not null)
        {
            message.DebtorName = DeepElementValue(pmtInf, ns, "Dbtr", "Nm") ?? string.Empty;
            message.DebtorAccount = DeepElementValue(pmtInf, ns, "DbtrAcct", "Id", "IBAN")
                                    ?? DeepElementValue(pmtInf, ns, "DbtrAcct", "Id", "Othr", "Id")
                                    ?? string.Empty;
            message.DebtorAgent = DeepElementValue(pmtInf, ns, "DbtrAgt", "FinInstnId", "BIC")
                                  ?? DeepElementValue(pmtInf, ns, "DbtrAgt", "FinInstnId", "BICFI")
                                  ?? string.Empty;

            var cdtTrfTxInf = pmtInf.Element(ns + "CdtTrfTxInf");
            if (cdtTrfTxInf is not null)
            {
                message.EndToEndId = DeepElementValue(cdtTrfTxInf, ns, "PmtId", "EndToEndId") ?? string.Empty;
                message.TransactionId = DeepElementValue(cdtTrfTxInf, ns, "PmtId", "InstrId") ?? string.Empty;

                var amtEl = cdtTrfTxInf.Descendants(ns + "InstdAmt").FirstOrDefault()
                            ?? cdtTrfTxInf.Descendants(ns + "Amt").FirstOrDefault();
                if (amtEl is not null)
                {
                    message.Amount = ParseDecimal(amtEl.Value);
                    message.Currency = amtEl.Attribute("Ccy")?.Value ?? string.Empty;
                }

                message.CreditorName = DeepElementValue(cdtTrfTxInf, ns, "Cdtr", "Nm") ?? string.Empty;
                message.CreditorAccount = DeepElementValue(cdtTrfTxInf, ns, "CdtrAcct", "Id", "IBAN")
                                          ?? DeepElementValue(cdtTrfTxInf, ns, "CdtrAcct", "Id", "Othr", "Id")
                                          ?? string.Empty;
                message.CreditorAgent = DeepElementValue(cdtTrfTxInf, ns, "CdtrAgt", "FinInstnId", "BIC")
                                        ?? DeepElementValue(cdtTrfTxInf, ns, "CdtrAgt", "FinInstnId", "BICFI")
                                        ?? string.Empty;

                message.RemittanceInformation = DeepElementValue(cdtTrfTxInf, ns, "RmtInf", "Ustrd") ?? string.Empty;
            }
        }

        return Result.Success(message);
    }

    private static XDocument GeneratePain001(Iso20022Message message)
    {
        var ns = Pain001Ns;
        var creationDate = message.CreationDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "Document",
                new XElement(ns + "CstmrCdtTrfInitn",
                    new XElement(ns + "GrpHdr",
                        new XElement(ns + "MsgId", message.MessageId),
                        new XElement(ns + "CreDtTm", creationDate),
                        new XElement(ns + "NbOfTxs", "1"),
                        new XElement(ns + "CtrlSum",
                            message.Amount.ToString("F2", CultureInfo.InvariantCulture))),
                    new XElement(ns + "PmtInf",
                        new XElement(ns + "PmtInfId", message.MessageId + "-PMT"),
                        new XElement(ns + "PmtMtd", "TRF"),
                        new XElement(ns + "NbOfTxs", "1"),
                        new XElement(ns + "ReqdExctnDt",
                            new XElement(ns + "Dt",
                                message.CreationDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))),
                        new XElement(ns + "Dbtr",
                            new XElement(ns + "Nm", message.DebtorName)),
                        new XElement(ns + "DbtrAcct",
                            new XElement(ns + "Id",
                                new XElement(ns + "Othr",
                                    new XElement(ns + "Id", message.DebtorAccount)))),
                        new XElement(ns + "DbtrAgt",
                            new XElement(ns + "FinInstnId",
                                new XElement(ns + "BICFI", message.DebtorAgent))),
                        new XElement(ns + "CdtTrfTxInf",
                            new XElement(ns + "PmtId",
                                new XElement(ns + "InstrId", message.TransactionId),
                                new XElement(ns + "EndToEndId", message.EndToEndId)),
                            new XElement(ns + "Amt",
                                new XElement(ns + "InstdAmt",
                                    new XAttribute("Ccy", message.Currency),
                                    message.Amount.ToString("F2", CultureInfo.InvariantCulture))),
                            new XElement(ns + "CdtrAgt",
                                new XElement(ns + "FinInstnId",
                                    new XElement(ns + "BICFI", message.CreditorAgent))),
                            new XElement(ns + "Cdtr",
                                new XElement(ns + "Nm", message.CreditorName)),
                            new XElement(ns + "CdtrAcct",
                                new XElement(ns + "Id",
                                    new XElement(ns + "Othr",
                                        new XElement(ns + "Id", message.CreditorAccount)))),
                            new XElement(ns + "RmtInf",
                                new XElement(ns + "Ustrd", message.RemittanceInformation)))))));
    }

    #endregion

    #region XML Helpers

    /// <summary>
    /// Finds the namespace of a descendant element by local name.
    /// </summary>
    private static XNamespace? FindNamespace(XDocument doc, string localName)
    {
        var element = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
        return element?.Name.Namespace;
    }

    /// <summary>
    /// Returns true if any descendant of root has the given local name.
    /// </summary>
    private static bool RootContains(XElement root, string localName)
    {
        return root.Descendants().Any(e => e.Name.LocalName == localName);
    }

    /// <summary>
    /// Gets the string value of a direct child element.
    /// </summary>
    private static string ElementValue(XElement parent, XNamespace ns, string localName)
    {
        return parent.Element(ns + localName)?.Value ?? string.Empty;
    }

    /// <summary>
    /// Navigates a chain of nested elements and returns the innermost value.
    /// </summary>
    private static string? DeepElementValue(XElement parent, XNamespace ns, params string[] path)
    {
        var current = parent;
        foreach (var name in path)
        {
            current = current.Element(ns + name);
            if (current is null)
            {
                return null;
            }
        }
        return current.Value;
    }

    private static DateTime ParseDateTime(string value)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
        {
            return dt;
        }
        return DateTime.UtcNow;
    }

    private static decimal ParseDecimal(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }
        return 0m;
    }

    #endregion
}
