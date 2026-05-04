using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Accounts.Domain.Entities;
using GoldBank.Core.Modules.BranchCash.Domain.Entities;

namespace GoldBank.Gateway.Services;

/// <summary>
/// Generates A6 PDF receipts for branch cash transactions (STORY-158).
/// PDFs are written to disk on first generation and reused on subsequent calls.
/// </summary>
public sealed class ReceiptPdfService
{
    private readonly GoldBankDbContext _db;
    private readonly string _storageRoot;

    static ReceiptPdfService()
    {
        // QuestPDF is free for projects with revenue < $1M; required by license terms
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReceiptPdfService(GoldBankDbContext db)
    {
        _db = db;
        _storageRoot = Environment.GetEnvironmentVariable("RECEIPTS_STORAGE_ROOT")
                       ?? "/var/lib/goldbank/receipts";
    }

    /// <summary>
    /// Returns the receipt PDF bytes for a cash transaction. Uses disk cache.
    /// </summary>
    public async Task<(byte[] Bytes, string FilePath)> GetOrCreateAsync(
        BranchCashTransaction txn,
        CancellationToken ct = default)
    {
        var path = BuildPath(txn);

        // Disk cache hit
        if (File.Exists(path))
        {
            var cached = await File.ReadAllBytesAsync(path, ct);
            return (cached, path);
        }

        // Generate
        var bytes = await BuildPdfAsync(txn, ct);

        // Cache to disk
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, ct);

        // Persist relative path on the row for traceability
        if (txn.ReceiptPdfPath != path)
        {
            var update = await _db.BranchCashTransactions
                .FirstOrDefaultAsync(t => t.Id == txn.Id, ct);
            if (update != null)
            {
                update.ReceiptPdfPath = path;
                update.UpdatedAt      = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
        }

        return (bytes, path);
    }

    private string BuildPath(BranchCashTransaction txn)
    {
        var month = txn.CreatedAt.ToString("yyyyMM");
        return Path.Combine(_storageRoot, txn.TenantId ?? "default", month, $"{txn.Id}.pdf");
    }

    // ─── PDF construction ──────────────────────────────────────────────────

    private async Task<byte[]> BuildPdfAsync(BranchCashTransaction txn, CancellationToken ct)
    {
        // Resolve display fields
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == txn.AccountId, ct);
        var teller  = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == txn.TellerId, ct);
        var branch  = await _db.Branches.FirstOrDefaultAsync(b => b.Id == txn.BranchId, ct);

        var customerName = account != null
            ? ((account.FirstName ?? "") + " " + (account.LastName ?? "")).Trim()
            : "Unknown";

        var denominations = ParseDenominations(txn.DenominationBreakdownJson);

        var qrPng = GenerateQrPng(BuildShortId("TXN", txn.TransactionId == Guid.Empty ? txn.Id : txn.TransactionId));

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A6);
                page.Margin(8);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(8).FontColor(Colors.Black));

                page.Content().Column(col =>
                {
                    // ── Header ──────────────────────────────────────────
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("GOLDBANK").FontSize(12).Bold();
                            c.Item().Text(branch?.Name ?? "Branch").FontSize(8);
                            if (!string.IsNullOrWhiteSpace(branch?.Address))
                                c.Item().Text(branch.Address).FontSize(7).Light();
                            if (!string.IsNullOrWhiteSpace(branch?.City))
                                c.Item().Text(branch.City).FontSize(7).Light();
                        });
                    });

                    col.Item().PaddingVertical(4).LineHorizontal(0.5f);

                    // ── Type label ──────────────────────────────────────
                    col.Item().AlignCenter().Text(txn.Direction.ToUpperInvariant() + " RECEIPT")
                        .FontSize(11).Bold();

                    col.Item().PaddingTop(4);

                    // ── Body fields ─────────────────────────────────────
                    col.Item().Column(body =>
                    {
                        Field(body, "Date",      txn.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
                        Field(body, "Reference", BuildShortId("TXN", txn.Id));
                        Field(body, "Teller",    teller != null ? $"{teller.Username} ({BuildShortId("TLR", teller.Id)})" : "—");
                        Field(body, "Account",   BuildShortId("ACC", txn.AccountId));
                        Field(body, "Holder",    customerName);
                        if (txn.Direction == "Deposit" && !string.IsNullOrWhiteSpace(txn.DepositorName))
                            Field(body, "Depositor", txn.DepositorName);
                        Field(body, "Currency",  txn.Currency);
                        body.Item().PaddingTop(2)
                            .Text($"Amount: {txn.Amount:N2} {txn.Currency}").FontSize(11).Bold();
                    });

                    col.Item().PaddingVertical(4).LineHorizontal(0.5f);

                    // ── Denomination breakdown ──────────────────────────
                    if (denominations.Count > 0)
                    {
                        col.Item().Text("Denominations").FontSize(8).Bold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(1);
                                c.RelativeColumn(2);
                            });
                            table.Header(h =>
                            {
                                h.Cell().Text("Denom").FontSize(7).SemiBold();
                                h.Cell().Text("Count").FontSize(7).SemiBold();
                                h.Cell().AlignRight().Text("Subtotal").FontSize(7).SemiBold();
                            });
                            decimal total = 0m;
                            foreach (var line in denominations.OrderByDescending(d => d.FaceValue))
                            {
                                var sub = line.FaceValue * line.Count;
                                total += sub;
                                table.Cell().Text(line.FaceValue.ToString("N2")).FontSize(7);
                                table.Cell().Text(line.Count.ToString()).FontSize(7);
                                table.Cell().AlignRight().Text(sub.ToString("N2")).FontSize(7);
                            }
                            table.Cell().Text("Total").FontSize(7).Bold();
                            table.Cell().Text("");
                            table.Cell().AlignRight().Text(total.ToString("N2")).FontSize(7).Bold();
                        });
                    }

                    col.Item().PaddingVertical(4).LineHorizontal(0.5f);

                    // ── Footer: QR + signatures ─────────────────────────
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(50).Image(qrPng);
                        row.RelativeItem().PaddingLeft(6).Column(c =>
                        {
                            c.Item().Text("Thank you for banking with us.").FontSize(7).Italic();
                            c.Item().PaddingTop(6).Text("Customer:_______________").FontSize(7);
                            c.Item().PaddingTop(6).Text("Teller:_________________").FontSize(7);
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private static void Field(ColumnDescriptor body, string label, string value)
    {
        body.Item().Row(row =>
        {
            row.RelativeItem(2).Text(label + ":").FontSize(7).Light();
            row.RelativeItem(3).Text(value ?? "—").FontSize(7);
        });
    }

    private static byte[] GenerateQrPng(string content)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(8);
    }

    private static List<DenominationLine> ParseDenominations(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<DenominationLine>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static string BuildShortId(string prefix, Guid id)
    {
        var hex = id.ToString("N");
        var lastPart = Convert.ToInt64(hex.Substring(24, 8), 16);
        return $"{prefix}-{lastPart:D6}";
    }

    private sealed record DenominationLine(decimal FaceValue, int Count, string? Type);
}
