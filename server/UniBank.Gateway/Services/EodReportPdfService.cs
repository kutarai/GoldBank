using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.BranchCash.Domain.Entities;

namespace UniBank.Gateway.Services;

/// <summary>
/// Generates the End-of-Day teller report (STORY-159) as an A4 PDF.
///
/// Includes:
///   * Header (tenant, branch, business date, teller)
///   * Opening float per currency w/ denomination breakdown
///   * Chronological transactions table
///   * Per-currency totals + expected closing
///   * Counted closing + variance
///   * Signature blocks (Teller, Supervisor)
///
/// Cached on disk; subsequent fetches serve from disk.
/// </summary>
public sealed class EodReportPdfService
{
    private readonly UniBankDbContext _db;
    private readonly string _storageRoot;

    public EodReportPdfService(UniBankDbContext db)
    {
        _db = db;
        _storageRoot = Environment.GetEnvironmentVariable("RECEIPTS_STORAGE_ROOT")
                       ?? "/var/lib/unibank/receipts";
    }

    /// <summary>
    /// Returns the report bytes for a drawer session, generating + caching if needed.
    /// </summary>
    public async Task<(byte[] Bytes, string FilePath)> GetOrCreateAsync(
        Guid drawerSessionId,
        CancellationToken ct = default)
    {
        var drawer = await _db.TellerDrawerSessions
            .FirstOrDefaultAsync(d => d.Id == drawerSessionId, ct);
        if (drawer == null)
            throw new InvalidOperationException($"Drawer session {drawerSessionId} not found");

        var path = BuildPath(drawer);

        // Disk cache hit
        if (File.Exists(path))
        {
            var cached = await File.ReadAllBytesAsync(path, ct);
            return (cached, path);
        }

        var bytes = await BuildPdfAsync(drawer, ct);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, ct);

        // Persist on the row
        if (drawer.EodReportPath != path)
        {
            drawer.EodReportPath = path;
            drawer.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return (bytes, path);
    }

    private string BuildPath(TellerDrawerSession drawer)
    {
        var month = drawer.BusinessDate.ToString("yyyyMM");
        return Path.Combine(_storageRoot, drawer.TenantId ?? "default", month, $"eod-{drawer.Id}.pdf");
    }

    // ─── PDF construction ──────────────────────────────────────────────────

    private async Task<byte[]> BuildPdfAsync(TellerDrawerSession drawer, CancellationToken ct)
    {
        var teller = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == drawer.TellerId, ct);
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == drawer.BranchId, ct);
        Guid? supId = drawer.ClosedBySupervisorId;
        var supervisor = supId.HasValue
            ? await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == supId.Value, ct)
            : null;

        // Day window
        var dayStart = drawer.BusinessDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd   = drawer.BusinessDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var txns = await _db.BranchCashTransactions
            .Where(t => t.DrawerSessionId == drawer.Id && t.CreatedAt >= dayStart && t.CreatedAt < dayEnd)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        // Parse opening / closing JSON
        var opening = ParseFloatJson(drawer.OpeningFloatJson);
        var closing = ParseFloatJson(drawer.ClosingBalanceJson);

        // Compute totals per currency
        var currencies = txns.Select(t => t.Currency)
            .Concat(opening.Keys)
            .Concat(closing.Keys)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var totalsByCurrency = currencies.ToDictionary(c => c, c => new CurrencyTotal
        {
            Currency = c,
            OpeningFloat = opening.TryGetValue(c, out var o) ? o.Total : 0m,
            Deposits     = txns.Where(t => t.Direction == "Deposit"    && t.Status == "completed" && t.Currency == c).Sum(t => t.Amount),
            Withdrawals  = txns.Where(t => t.Direction == "Withdrawal" && t.Status == "completed" && t.Currency == c).Sum(t => t.Amount),
            Reversals    = txns.Where(t => t.Direction == "Reversal"                                && t.Currency == c).Sum(t => t.Amount),
            CountedClosing = closing.TryGetValue(c, out var cl) ? cl.Total : 0m,
        });

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(Colors.Black));

                page.Header().Column(h =>
                {
                    h.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("UNIBANK").FontSize(16).Bold();
                            c.Item().Text(branch?.Name ?? "Branch").FontSize(11);
                            if (!string.IsNullOrWhiteSpace(branch?.Address))
                                c.Item().Text(branch.Address).FontSize(8).Light();
                        });
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("END-OF-DAY TELLER REPORT").FontSize(13).Bold();
                            c.Item().Text($"Business Date: {drawer.BusinessDate:yyyy-MM-dd}").FontSize(9);
                            c.Item().Text($"Teller: {teller?.FullName ?? teller?.Username ?? "—"} ({BuildShortId("TLR", drawer.TellerId)})").FontSize(9);
                            c.Item().Text($"Drawer: {BuildShortId("DRW", drawer.Id)}").FontSize(9);
                            c.Item().Text($"Opened: {drawer.OpenedAt:yyyy-MM-dd HH:mm}  ·  Closed: {(drawer.ClosedAt?.ToString("yyyy-MM-dd HH:mm") ?? "—")}").FontSize(8).Light();
                        });
                    });
                    h.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    // ── 1. Opening Float ────────────────────────────────
                    col.Item().Text("Opening Float").FontSize(11).Bold();
                    col.Item().PaddingTop(2);
                    if (opening.Count == 0)
                    {
                        col.Item().Text("(no opening float recorded)").Italic().FontSize(8);
                    }
                    else
                    {
                        foreach (var (cur, info) in opening)
                        {
                            col.Item().Text($"{cur}  {info.Total:N2}").FontSize(9).Bold();
                            if (info.Denominations.Count > 0)
                            {
                                var line = string.Join("   ",
                                    info.Denominations.OrderByDescending(d => d.FaceValue)
                                        .Select(d => $"{d.FaceValue:N2}×{d.Count}"));
                                col.Item().PaddingLeft(8).Text(line).FontSize(7).Light();
                            }
                        }
                    }

                    col.Item().PaddingTop(8);

                    // ── 2. Transactions table ───────────────────────────
                    col.Item().Text($"Transactions ({txns.Count})").FontSize(11).Bold();
                    col.Item().PaddingTop(2);

                    if (txns.Count == 0)
                    {
                        col.Item().Text("(no transactions on this drawer)").Italic().FontSize(8);
                    }
                    else
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(50);   // time
                                c.ConstantColumn(70);   // ref
                                c.ConstantColumn(60);   // type
                                c.ConstantColumn(70);   // account
                                c.ConstantColumn(35);   // currency
                                c.RelativeColumn(1);    // amount
                                c.ConstantColumn(60);   // status
                            });
                            table.Header(h =>
                            {
                                h.Cell().Text("Time").FontSize(7).SemiBold();
                                h.Cell().Text("Reference").FontSize(7).SemiBold();
                                h.Cell().Text("Type").FontSize(7).SemiBold();
                                h.Cell().Text("Account").FontSize(7).SemiBold();
                                h.Cell().Text("Cur").FontSize(7).SemiBold();
                                h.Cell().AlignRight().Text("Amount").FontSize(7).SemiBold();
                                h.Cell().Text("Status").FontSize(7).SemiBold();
                            });
                            foreach (var t in txns)
                            {
                                Cell(table, t.CreatedAt.ToString("HH:mm"));
                                Cell(table, BuildShortId("TXN", t.Id));
                                Cell(table, t.Direction);
                                Cell(table, BuildShortId("ACC", t.AccountId));
                                Cell(table, t.Currency);
                                table.Cell().AlignRight().Text(t.Amount.ToString("N2")).FontSize(7);
                                Cell(table, t.Status);
                            }
                        });
                    }

                    col.Item().PaddingTop(8);

                    // ── 3. Totals + variance ────────────────────────────
                    col.Item().Text("Totals & Variance").FontSize(11).Bold();
                    col.Item().PaddingTop(2);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(50);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("Cur").FontSize(7).SemiBold();
                            h.Cell().AlignRight().Text("Open Float").FontSize(7).SemiBold();
                            h.Cell().AlignRight().Text("Deposits +").FontSize(7).SemiBold();
                            h.Cell().AlignRight().Text("Withdrawals −").FontSize(7).SemiBold();
                            h.Cell().AlignRight().Text("Reversals").FontSize(7).SemiBold();
                            h.Cell().AlignRight().Text("Expected").FontSize(7).SemiBold();
                            h.Cell().AlignRight().Text("Counted").FontSize(7).SemiBold();
                            h.Cell().AlignRight().Text("Variance").FontSize(7).SemiBold();
                        });
                        foreach (var ct2 in totalsByCurrency.Values.OrderBy(t => t.Currency))
                        {
                            table.Cell().Text(ct2.Currency).FontSize(8).Bold();
                            table.Cell().AlignRight().Text(ct2.OpeningFloat.ToString("N2")).FontSize(8);
                            table.Cell().AlignRight().Text(ct2.Deposits.ToString("N2")).FontSize(8);
                            table.Cell().AlignRight().Text(ct2.Withdrawals.ToString("N2")).FontSize(8);
                            table.Cell().AlignRight().Text(ct2.Reversals.ToString("N2")).FontSize(8);
                            table.Cell().AlignRight().Text(ct2.ExpectedClosing.ToString("N2")).FontSize(8);
                            table.Cell().AlignRight().Text(ct2.CountedClosing.ToString("N2")).FontSize(8);
                            table.Cell().AlignRight().Text(text =>
                            {
                                var varianceText = ct2.Variance.ToString("N2");
                                if (ct2.Variance == 0)
                                    text.Span(varianceText).FontSize(8).FontColor(Colors.Green.Darken2);
                                else
                                    text.Span(varianceText).FontSize(8).Bold().FontColor(Colors.Red.Darken2);
                            });
                        }
                    });

                    col.Item().PaddingTop(20);

                    // ── 4. Signature blocks ─────────────────────────────
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Teller").FontSize(9).Bold();
                            c.Item().Text(teller?.FullName ?? teller?.Username ?? "—").FontSize(8);
                            c.Item().PaddingTop(20).LineHorizontal(0.5f);
                            c.Item().Text("Signature / Date").FontSize(7).Light();
                        });
                        row.ConstantItem(40);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Supervisor").FontSize(9).Bold();
                            c.Item().Text(supervisor?.FullName ?? supervisor?.Username ?? "(awaiting countersign)").FontSize(8);
                            c.Item().PaddingTop(20).LineHorizontal(0.5f);
                            c.Item().Text("Signature / Date").FontSize(7).Light();
                        });
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated ").FontSize(7).Light();
                    text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC")).FontSize(7).Light();
                    text.Span("  ·  Page ").FontSize(7).Light();
                    text.CurrentPageNumber().FontSize(7).Light();
                    text.Span(" of ").FontSize(7).Light();
                    text.TotalPages().FontSize(7).Light();
                });
            });
        }).GeneratePdf();
    }

    private static void Cell(TableDescriptor t, string text)
    {
        t.Cell().Text(text ?? "—").FontSize(7);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private static Dictionary<string, FloatInfo> ParseFloatJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, FloatInfo>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var info = new FloatInfo();
                if (prop.Value.TryGetProperty("total", out var totalEl))
                    info.Total = totalEl.GetDecimal();
                if (prop.Value.TryGetProperty("denominations", out var denomsEl) && denomsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var d in denomsEl.EnumerateArray())
                    {
                        var face = d.TryGetProperty("faceValue", out var f) ? f.GetDecimal() : 0m;
                        var count = d.TryGetProperty("count", out var c) ? c.GetInt32() : 0;
                        info.Denominations.Add(new DenomLine(face, count));
                    }
                }
                result[prop.Name] = info;
            }
            return result;
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

    private sealed class FloatInfo
    {
        public decimal Total { get; set; }
        public List<DenomLine> Denominations { get; set; } = new();
    }
    private sealed record DenomLine(decimal FaceValue, int Count);

    private sealed class CurrencyTotal
    {
        public string Currency { get; set; } = "";
        public decimal OpeningFloat { get; set; }
        public decimal Deposits { get; set; }
        public decimal Withdrawals { get; set; }
        public decimal Reversals { get; set; }
        public decimal CountedClosing { get; set; }
        public decimal ExpectedClosing => OpeningFloat + Deposits - Withdrawals;
        public decimal Variance => CountedClosing - ExpectedClosing;
    }
}
