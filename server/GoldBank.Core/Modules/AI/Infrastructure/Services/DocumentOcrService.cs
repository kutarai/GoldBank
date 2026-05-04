using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace GoldBank.Core.Modules.AI.Infrastructure.Services;

public sealed class IdDocumentFields
{
    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }

    [JsonPropertyName("id_number")]
    public string? IdNumber { get; set; }

    [JsonPropertyName("date_of_birth")]
    public string? DateOfBirth { get; set; }

    [JsonPropertyName("nationality")]
    public string? Nationality { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("expiry_date")]
    public string? ExpiryDate { get; set; }

    [JsonPropertyName("document_type")]
    public string? DocumentType { get; set; }
}

public sealed class ChequeFields
{
    [JsonPropertyName("cheque_number")]
    public string? ChequeNumber { get; set; }

    [JsonPropertyName("amount_figures")]
    public string? AmountFigures { get; set; }

    [JsonPropertyName("amount_words")]
    public string? AmountWords { get; set; }

    [JsonPropertyName("payee")]
    public string? Payee { get; set; }

    [JsonPropertyName("drawer")]
    public string? Drawer { get; set; }

    [JsonPropertyName("bank_name")]
    public string? BankName { get; set; }

    [JsonPropertyName("branch_code")]
    public string? BranchCode { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }
}

public sealed class BillFields
{
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("account_number")]
    public string? AccountNumber { get; set; }

    [JsonPropertyName("amount_due")]
    public string? AmountDue { get; set; }

    [JsonPropertyName("due_date")]
    public string? DueDate { get; set; }

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }
}

public sealed class ReceiptFields
{
    [JsonPropertyName("merchant_name")]
    public string? MerchantName { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("total_amount")]
    public string? TotalAmount { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("items")]
    public List<string>? Items { get; set; }
}

public sealed class PayslipFields
{
    [JsonPropertyName("employer")]
    public string? Employer { get; set; }

    [JsonPropertyName("employee_name")]
    public string? EmployeeName { get; set; }

    [JsonPropertyName("gross_salary")]
    public string? GrossSalary { get; set; }

    [JsonPropertyName("net_salary")]
    public string? NetSalary { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("pay_period")]
    public string? PayPeriod { get; set; }
}

public sealed class ProofOfAddressFields
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("document_date")]
    public string? DocumentDate { get; set; }

    [JsonPropertyName("document_type")]
    public string? DocumentType { get; set; }
}

public sealed class FieldComparisonResult
{
    public string FieldName { get; init; } = default!;
    public string? Expected { get; init; }
    public string? Extracted { get; init; }
    public bool IsMatch { get; init; }
}

public sealed class DocumentOcrService
{
    private readonly OllamaClient _ollamaClient;
    private readonly ILogger<DocumentOcrService> _logger;

    public DocumentOcrService(OllamaClient ollamaClient, ILogger<DocumentOcrService> logger)
    {
        _ollamaClient = ollamaClient;
        _logger = logger;
    }

    public async Task<IdDocumentFields> ExtractIdFieldsAsync(
        byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        const string prompt =
            "Extract the following fields from this identity document (Zimbabwe national ID, " +
            "biometric card, or passport). Return ONLY valid JSON:\n" +
            "{\"full_name\": \"...\", \"id_number\": \"...\", \"date_of_birth\": \"DD/MM/YYYY\", " +
            "\"nationality\": \"...\", \"gender\": \"Male/Female\", \"expiry_date\": \"DD/MM/YYYY or null\", " +
            "\"document_type\": \"national_id/biometric_card/passport\"}";

        return await _ollamaClient.ExtractFromImageAsync<IdDocumentFields>(
            prompt, imageBytes, cancellationToken);
    }

    public async Task<ChequeFields> ExtractChequeFieldsAsync(
        byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        const string prompt =
            "Extract all fields from this cheque image. Return ONLY valid JSON:\n" +
            "{\"cheque_number\": \"...\", \"amount_figures\": \"...\", \"amount_words\": \"...\", " +
            "\"payee\": \"...\", \"drawer\": \"...\", \"bank_name\": \"...\", " +
            "\"branch_code\": \"...\", \"date\": \"DD/MM/YYYY\", \"currency\": \"ZWG/USD\"}";

        return await _ollamaClient.ExtractFromImageAsync<ChequeFields>(
            prompt, imageBytes, cancellationToken);
    }

    public async Task<BillFields> ExtractBillFieldsAsync(
        byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        const string prompt =
            "Extract billing details from this utility bill or invoice. Return ONLY valid JSON:\n" +
            "{\"provider\": \"...\", \"account_number\": \"...\", \"amount_due\": \"...\", " +
            "\"due_date\": \"DD/MM/YYYY\", \"reference\": \"...\", \"currency\": \"ZWG/USD\"}";

        return await _ollamaClient.ExtractFromImageAsync<BillFields>(
            prompt, imageBytes, cancellationToken);
    }

    public async Task<ReceiptFields> ExtractReceiptFieldsAsync(
        byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        const string prompt =
            "Extract details from this receipt. Return ONLY valid JSON:\n" +
            "{\"merchant_name\": \"...\", \"date\": \"DD/MM/YYYY\", \"total_amount\": \"...\", " +
            "\"currency\": \"ZWG/USD\", \"category\": \"groceries/fuel/dining/electronics/clothing/other\", " +
            "\"items\": [\"item1\", \"item2\"]}";

        return await _ollamaClient.ExtractFromImageAsync<ReceiptFields>(
            prompt, imageBytes, cancellationToken);
    }

    public async Task<PayslipFields> ExtractPayslipFieldsAsync(
        byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        const string prompt =
            "Extract financial details from this payslip or salary statement. Return ONLY valid JSON:\n" +
            "{\"employer\": \"...\", \"employee_name\": \"...\", \"gross_salary\": \"...\", " +
            "\"net_salary\": \"...\", \"currency\": \"ZWG/USD\", \"pay_period\": \"...\"}";

        return await _ollamaClient.ExtractFromImageAsync<PayslipFields>(
            prompt, imageBytes, cancellationToken);
    }

    public async Task<ProofOfAddressFields> ExtractProofOfAddressAsync(
        byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        const string prompt =
            "Extract the name, physical address, and document date from this proof of address " +
            "(utility bill, bank statement, or lease agreement). Return ONLY valid JSON:\n" +
            "{\"name\": \"...\", \"address\": \"...\", \"document_date\": \"DD/MM/YYYY\", " +
            "\"document_type\": \"utility_bill/bank_statement/lease_agreement\"}";

        return await _ollamaClient.ExtractFromImageAsync<ProofOfAddressFields>(
            prompt, imageBytes, cancellationToken);
    }

    public List<FieldComparisonResult> CompareIdFields(
        IdDocumentFields extracted, string expectedName, string expectedIdNumber, DateTime? expectedDob)
    {
        var results = new List<FieldComparisonResult>();

        results.Add(new FieldComparisonResult
        {
            FieldName = "name",
            Expected = expectedName,
            Extracted = extracted.FullName,
            IsMatch = FuzzyNameMatch(expectedName, extracted.FullName)
        });

        results.Add(new FieldComparisonResult
        {
            FieldName = "id_number",
            Expected = expectedIdNumber,
            Extracted = extracted.IdNumber,
            IsMatch = NormalizeIdNumber(expectedIdNumber) == NormalizeIdNumber(extracted.IdNumber)
        });

        if (expectedDob.HasValue && extracted.DateOfBirth is not null)
        {
            var extractedDob = ParseDate(extracted.DateOfBirth);
            results.Add(new FieldComparisonResult
            {
                FieldName = "date_of_birth",
                Expected = expectedDob.Value.ToString("dd/MM/yyyy"),
                Extracted = extracted.DateOfBirth,
                IsMatch = extractedDob.HasValue &&
                          extractedDob.Value.Date == expectedDob.Value.Date
            });
        }

        return results;
    }

    private static bool FuzzyNameMatch(string? expected, string? extracted)
    {
        if (expected is null || extracted is null) return false;

        var a = NormalizeName(expected);
        var b = NormalizeName(extracted);

        if (a == b) return true;

        // Check if all parts of expected name appear in extracted
        var expectedParts = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var extractedParts = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var matchCount = expectedParts.Count(ep =>
            extractedParts.Any(xp => xp == ep || LevenshteinDistance(ep, xp) <= 1));

        return matchCount >= Math.Max(1, expectedParts.Length - 1);
    }

    private static string NormalizeName(string name)
        => Regex.Replace(name.ToLowerInvariant().Trim(), @"\s+", " ");

    private static string? NormalizeIdNumber(string? id)
        => id?.Replace(" ", "").Replace("-", "").ToUpperInvariant();

    private static DateTime? ParseDate(string? dateStr)
    {
        if (dateStr is null) return null;
        string[] formats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy"];
        return DateTime.TryParseExact(dateStr, formats, null,
            System.Globalization.DateTimeStyles.None, out var dt) ? dt : null;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        for (var j = 1; j <= m; j++)
        {
            var cost = s[i - 1] == t[j - 1] ? 0 : 1;
            d[i, j] = Math.Min(
                Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                d[i - 1, j - 1] + cost);
        }

        return d[n, m];
    }
}
