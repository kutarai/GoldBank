using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UniBank.Core.Common.Persistence;

namespace UniBank.Gateway.Services;

/// <summary>
/// Branch Vault End-of-Day report (STORY-169). Builds an A4 PDF showing
/// opening stock, every movement of the day, closing stock, and variance.
/// </summary>
public sealed class VaultReportPdfService
{
    private readonly UniBankDbContext _db;
    public VaultReportPdfService(UniBankDbContext db) { _db = db; }

    public async Task<byte[]> BuildAsync(Guid vaultId, DateOnly date, CancellationToken ct)
    {
        var vault = await _db.Vaults.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vaultId, ct)
            ?? throw new InvalidOperationException("vault_not_found");

        var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == vault.BranchId, ct);

        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd   = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var movements = await _db.VaultMovements.AsNoTracking()
            .Where(m => m.VaultId == vaultId && m.CreatedAt >= dayStart && m.CreatedAt < dayEnd)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        // Closing stock (current snapshot — assumes report run end-of-day)
        var stock = await (
            from s in _db.VaultDenominationStock.AsNoTracking()
            join d in _db.CurrencyDenominations.AsNoTracking() on s.DenominationId equals d.Id
            where s.VaultId == vaultId
            orderby d.Currency, d.DisplayOrder
            select new { s.Count, d.Currency, d.FaceValue, d.DenominationType }
        ).ToListAsync(ct);

        var spotChecks = await _db.VaultSpotChecks.AsNoTracking()
            .Where(s => s.VaultId == vaultId && s.CreatedAt >= dayStart && s.CreatedAt < dayEnd)
            .ToListAsync(ct);

        var bytes = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("BRANCH VAULT — END OF DAY").FontSize(16).SemiBold();
                    col.Item().Text($"{branch?.Name ?? ""}  ·  {vault.Name}").FontSize(11);
                    col.Item().Text($"Business date: {date:yyyy-MM-dd}").FontSize(10);
                    col.Item().PaddingVertical(6).LineHorizontal(0.5f);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    // Closing stock
                    col.Item().Text("Closing Stock").SemiBold();
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); });
                        t.Header(h =>
                        {
                            h.Cell().Text("Currency").SemiBold();
                            h.Cell().Text("Face").SemiBold();
                            h.Cell().Text("Type").SemiBold();
                            h.Cell().AlignRight().Text("Count").SemiBold();
                            h.Cell().AlignRight().Text("Value").SemiBold();
                        });
                        foreach (var s in stock)
                        {
                            t.Cell().Text(s.Currency);
                            t.Cell().Text(s.FaceValue.ToString("N2"));
                            t.Cell().Text(s.DenominationType);
                            t.Cell().AlignRight().Text(s.Count.ToString());
                            t.Cell().AlignRight().Text((s.FaceValue * s.Count).ToString("N2"));
                        }
                    });

                    // Movements
                    col.Item().PaddingTop(6).Text("Movements").SemiBold();
                    if (movements.Count == 0)
                        col.Item().Text("(no movements)").Italic();
                    else
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(0.7f); c.RelativeColumn(0.7f); c.RelativeColumn(1.2f); c.RelativeColumn(2); });
                        t.Header(h =>
                        {
                            h.Cell().Text("Time").SemiBold();
                            h.Cell().Text("Type").SemiBold();
                            h.Cell().Text("Dir").SemiBold();
                            h.Cell().Text("Ccy").SemiBold();
                            h.Cell().AlignRight().Text("Amount").SemiBold();
                            h.Cell().Text("Notes").SemiBold();
                        });
                        foreach (var m in movements)
                        {
                            t.Cell().Text(m.CreatedAt.ToLocalTime().ToString("HH:mm"));
                            t.Cell().Text(m.Type);
                            t.Cell().Text(m.Direction);
                            t.Cell().Text(m.Currency);
                            t.Cell().AlignRight().Text(m.TotalAmount.ToString("N2"));
                            t.Cell().Text(m.Notes ?? "");
                        }
                    });

                    // Spot checks
                    if (spotChecks.Count > 0)
                    {
                        col.Item().PaddingTop(6).Text("Spot Checks").SemiBold();
                        foreach (var s in spotChecks)
                        {
                            col.Item().Text($"{s.CreatedAt.ToLocalTime():HH:mm}  ·  {(s.HasVariance ? "Variance" : "Balanced")}  ·  adj movement: {s.AdjustmentMovementId?.ToString() ?? "—"}");
                        }
                    }

                    // Signatures
                    col.Item().PaddingTop(20).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f);
                            c.Item().Text("Vault Manager").FontSize(9);
                        });
                        r.ConstantItem(20);
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f);
                            c.Item().Text("Branch Supervisor").FontSize(9);
                        });
                    });
                });

                page.Footer().AlignCenter().Text($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm}Z").FontSize(8);
            });
        }).GeneratePdf();

        return bytes;
    }
}
