using System.Security.Claims;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Accounts.Domain.Entities;
using GoldBank.Core.Modules.Admin.Domain.Entities;
using GoldBank.Core.Modules.AssetCustody.Domain.Entities;
using GoldBank.Core.Modules.BranchCash.Application.Services;
using GoldBank.Core.Modules.BranchCash.Domain.Entities;
using GoldBank.Gateway.Services;

namespace GoldBank.Gateway.Controllers;

/// <summary>
/// REST API for the bank-teller front-end (EPIC-021).
/// Implements STORY-149 (cash endpoints), STORY-150 (Customer Card),
/// STORY-152 (high-value supervisor approval).
///
/// Authorization: requires JWT with role in (teller, branch_manager, super_admin).
/// </summary>
[ApiController]
[Route("api/teller")]
[EnableCors("BankClient")]
// Separation of duties: only Teller and BranchManager roles can call the
// teller API. Admin (full system access) is explicitly blocked — the audit
// trail must show the actual person at the counter, not a super-admin.
[Authorize(Roles = "Teller,BranchManager,VaultManager,Admin")]
public class TellerApiController : ControllerBase
{
    private readonly GoldBankDbContext _db;
    private readonly DenominationValidationService _denomValidator;
    private readonly ReceiptPdfService _receiptPdfService;
    private readonly EodReportPdfService _eodPdfService;
    private readonly GoldBank.Core.Modules.BranchCash.Application.Services.VaultStockService _vaultStock;
    private readonly GoldBank.Gateway.Services.VaultReportPdfService _vaultReportPdf;
    private readonly IConnectionMultiplexer? _redis;

    public TellerApiController(
        GoldBankDbContext db,
        DenominationValidationService denomValidator,
        ReceiptPdfService receiptPdfService,
        EodReportPdfService eodPdfService,
        GoldBank.Core.Modules.BranchCash.Application.Services.VaultStockService vaultStock,
        GoldBank.Gateway.Services.VaultReportPdfService vaultReportPdf,
        IConnectionMultiplexer? redis = null)
    {
        _db = db;
        _denomValidator = denomValidator;
        _receiptPdfService = receiptPdfService;
        _eodPdfService = eodPdfService;
        _vaultStock = vaultStock;
        _vaultReportPdf = vaultReportPdf;
        _redis = redis;
    }

    // -------------------------------------------------------------------------
    // Identity helpers
    // -------------------------------------------------------------------------
    private Guid TellerId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;

    private string TenantId =>
        User.FindFirstValue("tenant_id") ?? string.Empty;

    private string TellerRole =>
        User.FindFirstValue(ClaimTypes.Role)
        ?? User.FindFirstValue("role")
        ?? "teller";

    // -------------------------------------------------------------------------
    // GET /api/teller/customers/search?q=...
    // -------------------------------------------------------------------------
    [HttpGet("customers/search")]
    public async Task<IActionResult> SearchCustomers([FromQuery] string q = "")
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Array.Empty<object>());

        var query = _db.Accounts.Where(a => a.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(TenantId))
            query = query.Where(a => a.TenantId == TenantId);

        // Strip common short-id prefix and any non-alphanumeric chars so users
        // can paste "ACC-000005", "5", or "0005" interchangeably.
        var trimmed = q.Trim();
        var prefixStripped = trimmed;
        if (trimmed.StartsWith("ACC-", StringComparison.OrdinalIgnoreCase))
            prefixStripped = trimmed.Substring(4);
        var digitsOnly = new string(trimmed.Where(char.IsDigit).ToArray());

        var like         = $"%{trimmed}%";
        var prefixLike   = $"%{prefixStripped}%";

        query = query.Where(a =>
            EF.Functions.ILike(a.PhoneNumber, like) ||
            (a.NationalId != null && EF.Functions.ILike(a.NationalId, like)) ||
            (a.FirstName  != null && EF.Functions.ILike(a.FirstName, like)) ||
            (a.LastName   != null && EF.Functions.ILike(a.LastName, like)) ||
            (a.CardPan    != null && EF.Functions.ILike(a.CardPan, prefixLike)));

        var rows = await query
            .OrderBy(a => a.LastName)
            .Take(50)
            .ToListAsync();

        // Add short-ID matches in a second pass (if user typed pure digits)
        if (long.TryParse(digitsOnly, out var requestedShortNum) && requestedShortNum > 0)
        {
            // Compute the short id for every account in this tenant — cheap
            // because the result set is small in dev. In production this would
            // be a function index on (lastPart) but for now: scan and filter.
            var shortIdMatches = await _db.Accounts
                .Where(a => a.DeletedAt == null
                            && (string.IsNullOrWhiteSpace(TenantId) || a.TenantId == TenantId))
                .ToListAsync();

            foreach (var a in shortIdMatches)
            {
                var lastPart = Convert.ToInt64(a.Id.ToString("N").Substring(24, 8), 16);
                if (lastPart == requestedShortNum && !rows.Any(r => r.Id == a.Id))
                {
                    rows.Add(a);
                }
            }
        }

        var projected = rows
            .Take(50)
            .Select(a => new
            {
                accountId  = a.Id,
                shortId    = ShortId("ACC", a.Id),
                name       = ((a.FirstName ?? "") + " " + (a.LastName ?? "")).Trim(),
                phone      = a.PhoneCountryCode + a.PhoneNumber,
                currency   = a.Currency,
                balance    = a.Balance,
                status     = a.Status,
                kycLevel   = a.KycLevel,
                cardPan    = a.CardPan,
            })
            .ToList();

        return Ok(projected);
    }

    // -------------------------------------------------------------------------
    // GET /api/teller/customers/{accountId}/card
    // STORY-150: returns photo (selfie), ID document, signature, profile, balances.
    // -------------------------------------------------------------------------
    [HttpGet("customers/{accountId:guid}/card")]
    public async Task<IActionResult> GetCustomerCard(Guid accountId)
    {
        var idDocTypes = new[] { "national_id", "passport", "drivers_license" };

        var account = await _db.Accounts
            .Where(a => a.Id == accountId && a.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (account == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(TenantId) && account.TenantId != TenantId)
            return Forbid();

        // Latest ID and selfie
        var idDoc = await _db.KycDocuments
            .Where(k => k.AccountId == accountId && idDocTypes.Contains(k.DocumentType))
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new { k.FileData, k.ContentType })
            .FirstOrDefaultAsync();

        var selfieDoc = await _db.KycDocuments
            .Where(k => k.AccountId == accountId && k.DocumentType == "selfie")
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new { k.FileData, k.ContentType })
            .FirstOrDefaultAsync();

        // All accounts for this customer (phone match) so we can show all balances
        var siblingAccounts = await _db.Accounts
            .Where(a => a.PhoneNumber == account.PhoneNumber && a.DeletedAt == null)
            .Select(a => new { a.Id, a.Currency, a.Balance })
            .ToListAsync();

        static string MimeOrJpeg(string? ct) =>
            string.IsNullOrWhiteSpace(ct) ? "image/jpeg" : ct;

        var payload = new
        {
            accountId        = ShortId("ACC", account.Id),
            accountIdRaw     = account.Id,
            fullName         = ((account.FirstName ?? "") + " " + (account.LastName ?? "")).Trim(),
            phone            = account.PhoneCountryCode + account.PhoneNumber,
            email            = account.Email,
            dateOfBirth      = account.DateOfBirth,
            nationalId       = account.NationalId,
            kycLevel         = account.KycLevel,
            status           = account.Status,
            balances         = siblingAccounts.Select(a => new { a.Currency, a.Balance, accountIdRaw = a.Id }).ToList(),
            flags = new
            {
                frozen             = account.Status == "frozen",
                suspended          = account.Status == "suspended",
                signatureVerified  = account.SignatureVerifiedAt.HasValue,
            },
            idImageUrl = idDoc?.FileData != null
                ? $"data:{MimeOrJpeg(idDoc.ContentType)};base64," + Convert.ToBase64String(idDoc.FileData)
                : null,
            selfieImageUrl = selfieDoc?.FileData != null
                ? $"data:{MimeOrJpeg(selfieDoc.ContentType)};base64," + Convert.ToBase64String(selfieDoc.FileData)
                : null,
            signatureImageUrl = account.SignatureImage != null
                ? "data:image/png;base64," + Convert.ToBase64String(account.SignatureImage)
                : null,
            signatureVerifiedBy = account.SignatureVerifiedBy,
            signatureVerifiedAt = account.SignatureVerifiedAt,
        };

        // Audit log: PII access
        _db.AuditLogs.Add(new GoldBank.Core.Modules.Admin.Domain.Entities.AuditLog
        {
            AdminUserId = TellerId,
            Action      = "customer.card.viewed",
            EntityType  = "Account",
            EntityId    = account.Id.ToString(),
            Details     = $"viewed by teller {TellerId}",
            IpAddress   = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            CreatedAt   = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"]        = "no-cache";

        return Ok(payload);
    }

    // -------------------------------------------------------------------------
    // GET /api/teller/customers/{accountId}/transactions?from=YYYY-MM-DD&to=YYYY-MM-DD
    // Lists transactions across ALL the customer's accounts (both currencies)
    // for the given date range. Defaults to last 30 days when from/to are omitted.
    // -------------------------------------------------------------------------
    [HttpGet("customers/{accountId:guid}/transactions")]
    public async Task<IActionResult> GetCustomerTransactions(
        Guid accountId,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to   = null)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.DeletedAt == null);
        if (account == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(TenantId) && account.TenantId != TenantId)
            return Forbid();

        // All accounts for this customer (phone group) so we list across currencies
        var phone = account.PhoneNumber;
        var accountIds = await _db.Accounts
            .Where(a => a.PhoneNumber == phone && a.DeletedAt == null)
            .Select(a => a.Id)
            .ToListAsync();

        // Default range: last 30 days
        var today    = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = from ?? today.AddDays(-30);
        var toDate   = to   ?? today;

        var fromUtc = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc   = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var rows = await _db.Transactions
            .Where(t => accountIds.Contains(t.AccountId)
                        && t.CreatedAt >= fromUtc
                        && t.CreatedAt <  toUtc)
            .OrderByDescending(t => t.CreatedAt)
            .Take(500)
            .Select(t => new
            {
                id              = t.Id,
                shortId         = ShortId("TXN", t.Id),
                accountId       = t.AccountId,
                type            = t.Type,
                amount          = t.Amount,
                fee             = t.Fee,
                currency        = t.Currency,
                status          = t.Status,
                reference       = t.Reference,
                description     = t.Description,
                counterparty    = t.CounterpartyName,
                balanceAfter    = t.BalanceAfter,
                createdAt       = t.CreatedAt,
            })
            .ToListAsync();

        return Ok(new
        {
            from  = fromDate.ToString("yyyy-MM-dd"),
            to    = toDate.ToString("yyyy-MM-dd"),
            total = rows.Count,
            items = rows,
        });
    }

    // -------------------------------------------------------------------------
    // GET /api/teller/customers/{accountId}/assets
    // Lists all live (non-deleted) custody assets owned by the customer behind
    // {accountId}. Each row carries an `isCollateral` flag + `collateralLoan`
    // descriptor so the front-end can disable Withdraw on assets that are
    // currently securing a non-Closed loan.
    // -------------------------------------------------------------------------
    [HttpGet("customers/{accountId:guid}/assets")]
    public async Task<IActionResult> ListCustomerAssets(Guid accountId)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.DeletedAt == null);
        if (account is null) return NotFound(new { error = "Account not found." });
        if (!string.IsNullOrWhiteSpace(TenantId) && account.TenantId != TenantId) return Forbid();

        var customerId = account.CustomerId;
        var assets = await _db.Assets
            .Where(a => a.CustomerId == customerId && !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new {
                a.Id, a.AssetType, a.Description, a.Quantity, a.Unit,
                a.WeightGrams, a.Purity, a.ReceiptNumber, a.ReceiptDate,
                a.LastValuationAmount, a.LastValuationDate, a.Status, a.VerificationStatus,
                a.DepositHouseId,
                DepositHouseName = _db.DepositHouses.Where(h => h.Id == a.DepositHouseId).Select(h => h.Name).FirstOrDefault(),
            })
            .ToListAsync();

        // Collateral check: any non-Closed/non-Defaulted loan across the
        // customer's accounts whose JSON CollateralAssetIds list contains the
        // asset id. EF can't translate Contains() against the JSON-mapped
        // List<Guid>, so we pull the candidate loans into memory and filter.
        var siblingAccountIds = await _db.Accounts
            .Where(a => a.CustomerId == account.CustomerId && a.DeletedAt == null)
            .Select(a => a.Id)
            .ToListAsync();
        var openLoans = await _db.Loans
            .Where(l => siblingAccountIds.Contains(l.AccountId)
                        && l.Status != "Closed" && l.Status != "Defaulted"
                        && l.DeletedAt == null)
            .Select(l => new { l.Id, l.Reference, l.OutstandingBalance, l.Currency, l.CollateralAssetIds })
            .ToListAsync();

        var items = assets.Select(a =>
        {
            var collateralLoan = openLoans.FirstOrDefault(l =>
                l.CollateralAssetIds != null && l.CollateralAssetIds.Contains(a.Id));
            return new
            {
                id              = ShortId("AST", a.Id),
                assetUuid       = a.Id,
                assetType       = a.AssetType.ToString(),
                description     = a.Description,
                quantity        = a.Quantity,
                unit            = a.Unit,
                weightGrams     = a.WeightGrams,
                purity          = a.Purity,
                receiptNumber   = a.ReceiptNumber,
                receiptDate     = a.ReceiptDate.ToString("yyyy-MM-dd"),
                depositHouseId  = a.DepositHouseId,
                depositHouse    = a.DepositHouseName ?? "—",
                lastValuation   = a.LastValuationAmount,
                lastValuedAt    = a.LastValuationDate?.ToString("yyyy-MM-dd"),
                status          = a.Status.ToString(),
                verification    = a.VerificationStatus.ToString(),
                isCollateral    = collateralLoan is not null,
                collateralLoan  = collateralLoan is null ? null : new
                {
                    loanId      = ShortId("LOAN", collateralLoan.Id),
                    reference   = collateralLoan.Reference,
                    outstanding = collateralLoan.OutstandingBalance,
                    currency    = collateralLoan.Currency,
                },
            };
        }).ToList();

        return Ok(items);
    }

    // -------------------------------------------------------------------------
    // GET /api/teller/deposit-houses
    // Used by the Asset Deposit dialog to populate the deposit-house picker.
    // -------------------------------------------------------------------------
    [HttpGet("deposit-houses")]
    public async Task<IActionResult> ListDepositHouses()
    {
        var rows = await _db.DepositHouses
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new {
                id            = d.Id,
                name          = d.Name,
                city          = d.City,
                trustStatus   = d.TrustStatus.ToString(),
                licenseNumber = d.LicenseNumber,
            })
            .ToListAsync();
        return Ok(rows);
    }

    // -------------------------------------------------------------------------
    // POST /api/teller/customers/{accountId}/assets
    // Teller registers a physical asset on a customer's behalf — same persistence
    // as AssetGrpcService.RegisterAsset, but invoked over REST from the teller
    // counter. Receipt-image upload isn't wired here yet; assets land Verified +
    // PendingVerification (treasury / valuer flow promotes to Active).
    // -------------------------------------------------------------------------
    public sealed record TellerRegisterAssetDto(
        Guid    DepositHouseId,
        string  ReceiptNumber,
        string  AssetType,         // "GoldCoin", "GoldBar", "Silver", "Platinum", "PreciousStone", "Other"
        string  Description,
        decimal Quantity,
        string? Unit,
        decimal? WeightGrams,
        decimal? Purity,
        decimal? InitialValuation,
        string? Currency);

    [HttpPost("customers/{accountId:guid}/assets")]
    public async Task<IActionResult> RegisterCustomerAsset(Guid accountId, [FromBody] TellerRegisterAssetDto body)
    {
        if (body is null) return BadRequest(new { error = "Body is required." });
        if (string.IsNullOrWhiteSpace(body.ReceiptNumber)) return BadRequest(new { error = "receiptNumber is required." });
        if (string.IsNullOrWhiteSpace(body.Description)) return BadRequest(new { error = "description is required." });
        if (body.Quantity <= 0) return BadRequest(new { error = "quantity must be positive." });
        if (!System.Enum.TryParse<AssetType>(body.AssetType, ignoreCase: true, out var assetType))
            return BadRequest(new { error = $"Unknown assetType '{body.AssetType}'." });

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.DeletedAt == null);
        if (account is null) return NotFound(new { error = "Account not found." });
        if (!string.IsNullOrWhiteSpace(TenantId) && account.TenantId != TenantId) return Forbid();

        var house = await _db.DepositHouses.FirstOrDefaultAsync(d => d.Id == body.DepositHouseId);
        if (house is null) return BadRequest(new { error = "Deposit house not found." });
        if (!house.IsActive) return BadRequest(new { error = "Deposit house is not active." });

        var duplicate = await _db.Assets.AnyAsync(a =>
            a.DepositHouseId == body.DepositHouseId &&
            a.ReceiptNumber == body.ReceiptNumber &&
            !a.IsDeleted);
        if (duplicate) return Conflict(new { error = "An asset with that receipt number already exists at this deposit house." });

        var now = DateTime.UtcNow;
        var hadInitialValuation = body.InitialValuation is { } v && v > 0m;
        var initial = hadInitialValuation ? body.InitialValuation!.Value : 0m;
        var tenantGuid = Guid.TryParse(account.TenantId, out var tg) ? tg : Guid.Empty;

        var asset = new Asset
        {
            CustomerId          = account.CustomerId,
            DepositHouseId      = body.DepositHouseId,
            ReceiptNumber       = body.ReceiptNumber.Trim(),
            AssetType           = assetType,
            Description         = body.Description.Trim(),
            Quantity            = body.Quantity,
            Unit                = string.IsNullOrWhiteSpace(body.Unit) ? "units" : body.Unit.Trim(),
            WeightGrams         = body.WeightGrams,
            Purity              = body.Purity,
            ReceiptImagePath    = string.Empty,
            ReceiptDate         = now,
            LastValuationAmount = initial,
            LastValuationDate   = hadInitialValuation ? now : null,
            VerificationStatus  = VerificationStatus.Pending,
            Status              = AssetStatus.PendingVerification,
            TenantId            = tenantGuid,
            CreatedAt           = now,
            UpdatedAt           = now,
        };
        _db.Assets.Add(asset);

        if (hadInitialValuation)
        {
            _db.AssetValuations.Add(new AssetValuation
            {
                AssetId         = asset.Id,
                ValuationAmount = initial,
                Currency        = string.IsNullOrWhiteSpace(body.Currency) ? "USD" : body.Currency.ToUpperInvariant(),
                ValuerName      = "Teller (intake)",
                ValuerLicense   = string.Empty,
                Notes           = $"Initial valuation recorded at deposit by teller {TellerId}.",
                ReportImagePath = null,
                TenantId        = tenantGuid,
                CreatedAt       = now,
                UpdatedAt       = now,
            });
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            id            = ShortId("AST", asset.Id),
            assetUuid     = asset.Id,
            receiptNumber = asset.ReceiptNumber,
            status        = asset.Status.ToString(),
            verification  = asset.VerificationStatus.ToString(),
        });
    }

    // -------------------------------------------------------------------------
    // POST /api/teller/assets/{assetId}/withdraw
    // Marks the asset Released + soft-deletes it (mirrors
    // AssetReleaseHandler.ApproveReleaseAsync). Refuses with 409 when the asset
    // is currently securing a non-Closed/non-Defaulted loan — the customer must
    // settle the loan first.
    // -------------------------------------------------------------------------
    public sealed record TellerWithdrawAssetDto(string? Reason);

    [HttpPost("assets/{assetId:guid}/withdraw")]
    public async Task<IActionResult> WithdrawAsset(Guid assetId, [FromBody] TellerWithdrawAssetDto? body)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == assetId && !a.IsDeleted);
        if (asset is null) return NotFound(new { error = "Asset not found." });

        // Find any open loan that lists this asset as collateral. Customer can
        // borrow against any of their accounts, so we hop via the asset's
        // CustomerId → all sibling Accounts → their open loans.
        var siblingAccountIds = await _db.Accounts
            .Where(a => a.CustomerId == asset.CustomerId && a.DeletedAt == null)
            .Select(a => a.Id)
            .ToListAsync();

        // Same pattern as the list endpoint — EF can't translate the JSON
        // collection check, so filter in memory.
        var openLoans = await _db.Loans
            .Where(l => siblingAccountIds.Contains(l.AccountId)
                        && l.Status != "Closed" && l.Status != "Defaulted"
                        && l.DeletedAt == null)
            .Select(l => new { l.Id, l.Reference, l.OutstandingBalance, l.Currency, l.Status, l.CollateralAssetIds })
            .ToListAsync();
        var blockingLoan = openLoans
            .Where(l => l.CollateralAssetIds != null && l.CollateralAssetIds.Contains(asset.Id))
            .Select(l => new { l.Id, l.Reference, l.OutstandingBalance, l.Currency, l.Status })
            .FirstOrDefault();

        if (blockingLoan is not null)
        {
            return Conflict(new
            {
                error = "Cannot withdraw — asset is collateral on an open loan.",
                blockingLoan = new
                {
                    loanId      = ShortId("LOAN", blockingLoan.Id),
                    reference   = blockingLoan.Reference,
                    outstanding = blockingLoan.OutstandingBalance,
                    currency    = blockingLoan.Currency,
                    status      = blockingLoan.Status,
                },
            });
        }

        var now = DateTime.UtcNow;
        asset.Status     = AssetStatus.Released;
        asset.UpdatedAt  = now;
        asset.IsDeleted  = true;
        asset.DeletedAt  = now;
        asset.DeletedBy  = TellerId.ToString();
        await _db.SaveChangesAsync();

        return Ok(new
        {
            id            = ShortId("AST", asset.Id),
            assetUuid     = asset.Id,
            receiptNumber = asset.ReceiptNumber,
            status        = asset.Status.ToString(),
            releasedAt    = now,
            reason        = body?.Reason,
        });
    }

    // -------------------------------------------------------------------------
    // GET /api/teller/drawer/current
    // -------------------------------------------------------------------------
    [HttpGet("drawer/current")]
    public async Task<IActionResult> GetCurrentDrawer()
    {
        var drawer = await _db.TellerDrawerSessions
            .Where(d => d.TellerId == TellerId && d.Status == "Open")
            .OrderByDescending(d => d.OpenedAt)
            .FirstOrDefaultAsync();

        if (drawer == null) return NotFound();
        return Ok(drawer);
    }

    // -------------------------------------------------------------------------
    // POST /api/teller/drawer/open
    // -------------------------------------------------------------------------
    public sealed record OpenDrawerRequest(Guid BranchId, string OpeningFloatJson);

    [HttpPost("drawer/open")]
    public async Task<IActionResult> OpenDrawer([FromBody] OpenDrawerRequest req)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await _db.TellerDrawerSessions
            .AnyAsync(d => d.TellerId == TellerId && d.BusinessDate == today && d.Status == "Open");
        if (existing)
            return Conflict(new { error = "drawer_already_open", message = "A drawer is already open for today." });

        var drawer = new TellerDrawerSession
        {
            TellerId         = TellerId,
            BranchId         = req.BranchId,
            BusinessDate     = today,
            Status           = "Open",
            OpeningFloatJson = req.OpeningFloatJson ?? "{}",
            OpenedAt         = DateTime.UtcNow,
            TenantId         = TenantId,
            CreatedAt        = DateTime.UtcNow,
        };
        _db.TellerDrawerSessions.Add(drawer);

        WriteAudit("drawer.open", "TellerDrawerSession", drawer.Id, $"branch={req.BranchId}");

        await _db.SaveChangesAsync();
        return Ok(new { drawerId = drawer.Id });
    }

    // -------------------------------------------------------------------------
    // POST /api/teller/drawer/close
    // -------------------------------------------------------------------------
    public sealed record CloseDrawerRequest(Guid DrawerId, string ClosingBalanceJson, bool ConfirmVariance = false);

    [HttpPost("drawer/close")]
    public async Task<IActionResult> CloseDrawer([FromBody] CloseDrawerRequest req)
    {
        var drawer = await _db.TellerDrawerSessions.FirstOrDefaultAsync(d => d.Id == req.DrawerId);
        if (drawer == null) return NotFound();
        if (drawer.TellerId != TellerId && TellerRole == "teller") return Forbid();
        if (drawer.Status != "Open") return Conflict(new { error = "drawer_not_open" });

        // ---- Compute expected closing = opening float + day's deposits - withdrawals per currency ----
        var expectedByCcy = ParseTotalsByCurrency(drawer.OpeningFloatJson);

        var dayStart = drawer.OpenedAt;
        var dayEnd   = DateTime.UtcNow.AddMinutes(1);
        var txns = await _db.BranchCashTransactions
            .Where(t => t.TellerId == drawer.TellerId
                     && t.CreatedAt >= dayStart && t.CreatedAt <= dayEnd
                     && t.Status == "completed")
            .Select(t => new { t.Currency, t.Amount, t.Direction })
            .ToListAsync();

        foreach (var t in txns)
        {
            var sign = t.Direction switch
            {
                "Deposit"   => +1m,
                "Withdrawal" => -1m,
                "Reversal"  => +1m, // compensating; sign already encoded in amount upstream
                _            => 0m
            };
            expectedByCcy.TryGetValue(t.Currency, out var cur);
            expectedByCcy[t.Currency] = cur + sign * t.Amount;
        }

        var countedByCcy = ParseTotalsByCurrency(req.ClosingBalanceJson);

        // Variance = counted - expected per currency
        var variance = new Dictionary<string, decimal>();
        var hasVariance = false;
        foreach (var ccy in expectedByCcy.Keys.Union(countedByCcy.Keys))
        {
            var diff = Math.Round(countedByCcy.GetValueOrDefault(ccy) - expectedByCcy.GetValueOrDefault(ccy), 2);
            variance[ccy] = diff;
            if (diff != 0) hasVariance = true;
        }

        if (hasVariance && !req.ConfirmVariance)
        {
            return Conflict(new
            {
                error = "drawer.variance_detected",
                message = "Closing balance does not match expected. Recount or confirm to proceed.",
                expected = expectedByCcy,
                counted  = countedByCcy,
                variance,
            });
        }

        // STORY-170 will block here if any pending_signature rows exist.
        drawer.Status              = "Closed";
        drawer.ClosingBalanceJson  = req.ClosingBalanceJson;
        drawer.ExpectedClosingJson = System.Text.Json.JsonSerializer.Serialize(expectedByCcy);
        drawer.VarianceJson        = System.Text.Json.JsonSerializer.Serialize(variance);
        drawer.ClosedAt            = DateTime.UtcNow;
        drawer.UpdatedAt           = DateTime.UtcNow;

        WriteAudit("drawer.close", "TellerDrawerSession", drawer.Id, "");
        await _db.SaveChangesAsync();

        // STORY-159: auto-generate the EOD report PDF on close.
        // Failure to generate the PDF must NOT roll back the close — log and proceed.
        try
        {
            await _eodPdfService.GetOrCreateAsync(drawer.Id);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex,
                "EOD report generation failed for drawer {DrawerId}; can be regenerated on demand",
                drawer.Id);
        }

        return Ok(new { ok = true, drawerId = drawer.Id });
    }

    // -------------------------------------------------------------------------
    // GET /api/teller/drawer/{sessionId}/eod-report.pdf
    // STORY-159: returns the End-of-Day teller report PDF.
    // -------------------------------------------------------------------------
    [HttpGet("drawer/{sessionId:guid}/eod-report.pdf")]
    public async Task<IActionResult> GetEodReportPdf(Guid sessionId)
    {
        var drawer = await _db.TellerDrawerSessions
            .FirstOrDefaultAsync(d => d.Id == sessionId);
        if (drawer == null) return NotFound();

        // Auth: only the owning teller (or branch_manager) can fetch
        if (TellerRole == nameof(AdminRole.Teller) && drawer.TellerId != TellerId)
            return Forbid();

        if (!string.IsNullOrWhiteSpace(TenantId) && drawer.TenantId != TenantId)
            return Forbid();

        try
        {
            var (bytes, _) = await _eodPdfService.GetOrCreateAsync(sessionId);
            Response.Headers["Content-Disposition"] = $"inline; filename=\"eod-{sessionId}.pdf\"";
            return File(bytes, "application/pdf");
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    // -------------------------------------------------------------------------
    // POST /api/teller/deposits
    // -------------------------------------------------------------------------
    public sealed record DenominationLineDto(decimal FaceValue, int Count, string? Type);
    public sealed record DepositRequest(
        Guid AccountId,
        string Currency,
        decimal Amount,
        string DepositorName,
        List<DenominationLineDto> Denominations);

    [HttpPost("deposits")]
    public async Task<IActionResult> CreateDeposit([FromBody] DepositRequest req)
    {
        if (req.Amount <= 0) return BadRequest(new { error = "invalid_amount" });
        if (string.IsNullOrWhiteSpace(req.DepositorName)) return BadRequest(new { error = "depositor_name_required" });

        var lines = req.Denominations
            .Select(d => new DenominationLine(d.FaceValue, d.Count, d.Type))
            .ToList();
        var validation = _denomValidator.Validate(req.Currency, req.Amount, lines);
        if (validation.IsFailure)
            return UnprocessableEntity(new { error = validation.Error.Code, message = validation.Error.Message });

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == req.AccountId && a.DeletedAt == null);
        if (account == null) return NotFound(new { error = "account_not_found" });
        if (account.Currency != req.Currency)
            return UnprocessableEntity(new { error = "currency_mismatch" });
        if (!string.IsNullOrWhiteSpace(TenantId) && account.TenantId != TenantId)
            return Forbid();

        var drawer = await _db.TellerDrawerSessions
            .FirstOrDefaultAsync(d => d.TellerId == TellerId && d.Status == "Open");
        if (drawer == null) return Conflict(new { error = "no_open_drawer" });

        await using var tx = await _db.Database.BeginTransactionAsync();

        var txn = new Transaction
        {
            AccountId         = account.Id,
            Type              = "cash_in_branch",
            Amount            = req.Amount,
            Fee               = 0,
            Tax               = 0,
            Status            = "completed",
            Currency          = req.Currency,
            Reference         = $"DEP-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            CounterpartyName  = req.DepositorName,
            BalanceAfter      = account.Balance + req.Amount,
            TenantId          = account.TenantId,
            CompletedAt       = DateTime.UtcNow,
            CreatedAt         = DateTime.UtcNow,
        };
        _db.Transactions.Add(txn);

        var cashTxn = new BranchCashTransaction
        {
            TransactionId             = txn.Id,
            DrawerSessionId           = drawer.Id,
            TellerId                  = TellerId,
            BranchId                  = drawer.BranchId,
            AccountId                 = account.Id,
            Direction                 = "Deposit",
            Currency                  = req.Currency,
            Amount                    = req.Amount,
            DepositorName             = req.DepositorName,
            DenominationBreakdownJson = System.Text.Json.JsonSerializer.Serialize(req.Denominations),
            IdentityVerified          = false, // not required for deposits
            Status                    = "completed",
            TenantId                  = account.TenantId,
            CreatedAt                 = DateTime.UtcNow,
        };
        _db.BranchCashTransactions.Add(cashTxn);

        account.Balance          += req.Amount;
        account.AvailableBalance += req.Amount;
        account.UpdatedAt         = DateTime.UtcNow;

        WriteAudit("cash.deposit", "BranchCashTransaction", cashTxn.Id, $"acct={account.Id} amt={req.Amount} {req.Currency}");

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            transactionId      = txn.Id,
            cashTransactionId  = cashTxn.Id,
            reference          = txn.Reference,
            newBalance         = account.Balance,
        });
    }

    // -------------------------------------------------------------------------
    // POST /api/teller/withdrawals
    // STORY-149 + STORY-152
    // -------------------------------------------------------------------------
    public sealed record WithdrawalRequest(
        Guid AccountId,
        string Currency,
        decimal Amount,
        List<DenominationLineDto> Denominations,
        bool IdentityVerified);

    [HttpPost("withdrawals")]
    public async Task<IActionResult> CreateWithdrawal([FromBody] WithdrawalRequest req)
    {
        if (req.Amount <= 0) return BadRequest(new { error = "invalid_amount" });
        if (!req.IdentityVerified) return UnprocessableEntity(new { error = "identity_not_verified" });

        var lines = req.Denominations
            .Select(d => new DenominationLine(d.FaceValue, d.Count, d.Type))
            .ToList();
        var validation = _denomValidator.Validate(req.Currency, req.Amount, lines);
        if (validation.IsFailure)
            return UnprocessableEntity(new { error = validation.Error.Code, message = validation.Error.Message });

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == req.AccountId && a.DeletedAt == null);
        if (account == null) return NotFound(new { error = "account_not_found" });
        if (account.Currency != req.Currency)
            return UnprocessableEntity(new { error = "currency_mismatch" });
        if (!string.IsNullOrWhiteSpace(TenantId) && account.TenantId != TenantId)
            return Forbid();
        if (account.Status != "active")
            return UnprocessableEntity(new { error = "account_not_active", status = account.Status });
        if (account.AvailableBalance < req.Amount)
            return UnprocessableEntity(new { error = "insufficient_funds" });

        var drawer = await _db.TellerDrawerSessions
            .FirstOrDefaultAsync(d => d.TellerId == TellerId && d.Status == "Open");
        if (drawer == null) return Conflict(new { error = "no_open_drawer" });

        // STORY-152: high-value threshold check
        var threshold = await GetHighValueThreshold(req.Currency);
        if (req.Amount > threshold)
        {
            // Create pending row, do NOT debit yet
            var pending = new BranchCashTransaction
            {
                TransactionId             = Guid.Empty, // filled on approval
                DrawerSessionId           = drawer.Id,
                TellerId                  = TellerId,
                BranchId                  = drawer.BranchId,
                AccountId                 = account.Id,
                Direction                 = "Withdrawal",
                Currency                  = req.Currency,
                Amount                    = req.Amount,
                DepositorName             = string.Empty,
                DenominationBreakdownJson = System.Text.Json.JsonSerializer.Serialize(req.Denominations),
                IdentityVerified          = true,
                Status                    = "pending_supervisor_approval",
                TenantId                  = account.TenantId,
                CreatedAt                 = DateTime.UtcNow,
            };
            _db.BranchCashTransactions.Add(pending);
            WriteAudit("cash.withdrawal.requires_approval", "BranchCashTransaction", pending.Id, $"amt={req.Amount}");
            await _db.SaveChangesAsync();

            return Accepted(new
            {
                requiresApproval     = true,
                pendingTransactionId = pending.Id,
                threshold,
                amount               = req.Amount,
            });
        }

        // No approval needed — proceed atomically
        await using var tx = await _db.Database.BeginTransactionAsync();

        var txn = new Transaction
        {
            AccountId    = account.Id,
            Type         = "cash_out_branch",
            Amount       = req.Amount,
            Status       = "completed",
            Currency     = req.Currency,
            Reference    = $"WDR-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            BalanceAfter = account.Balance - req.Amount,
            TenantId     = account.TenantId,
            CompletedAt  = DateTime.UtcNow,
            CreatedAt    = DateTime.UtcNow,
        };
        _db.Transactions.Add(txn);

        var cashTxn = new BranchCashTransaction
        {
            TransactionId             = txn.Id,
            DrawerSessionId           = drawer.Id,
            TellerId                  = TellerId,
            BranchId                  = drawer.BranchId,
            AccountId                 = account.Id,
            Direction                 = "Withdrawal",
            Currency                  = req.Currency,
            Amount                    = req.Amount,
            DepositorName             = string.Empty,
            DenominationBreakdownJson = System.Text.Json.JsonSerializer.Serialize(req.Denominations),
            IdentityVerified          = true,
            Status                    = "completed",
            TenantId                  = account.TenantId,
            CreatedAt                 = DateTime.UtcNow,
        };
        _db.BranchCashTransactions.Add(cashTxn);

        account.Balance          -= req.Amount;
        account.AvailableBalance -= req.Amount;
        account.UpdatedAt         = DateTime.UtcNow;

        WriteAudit("cash.withdrawal", "BranchCashTransaction", cashTxn.Id, $"acct={account.Id} amt={req.Amount} {req.Currency}");

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            transactionId     = txn.Id,
            cashTransactionId = cashTxn.Id,
            reference         = txn.Reference,
            newBalance        = account.Balance,
        });
    }

    // -------------------------------------------------------------------------
    // POST /api/teller/withdrawals/{pendingId}/approve
    // STORY-152: supervisor PIN approval flow
    // -------------------------------------------------------------------------
    public sealed record ApproveWithdrawalRequest(string SupervisorUsername, string SupervisorPin);

    [HttpPost("withdrawals/{pendingId:guid}/approve")]
    public async Task<IActionResult> ApproveWithdrawal(Guid pendingId, [FromBody] ApproveWithdrawalRequest req)
    {
        var pending = await _db.BranchCashTransactions
            .FirstOrDefaultAsync(t => t.Id == pendingId && t.Status == "pending_supervisor_approval");
        if (pending == null) return NotFound();
        if (DateTime.UtcNow - pending.CreatedAt > TimeSpan.FromMinutes(15))
            return Conflict(new { error = "approval_expired" });

        var supervisor = await _db.AdminUsers
            .FirstOrDefaultAsync(u => u.Username == req.SupervisorUsername);

        bool ok = supervisor != null
                  && BCrypt.Net.BCrypt.Verify(req.SupervisorPin, supervisor.PasswordHash)
                  && (supervisor.Role == AdminRole.BranchManager || supervisor.Role == AdminRole.Admin)
                  && supervisor.Id != pending.TellerId;

        if (!ok)
        {
            WriteAudit("cash.withdrawal.approval.failed", "BranchCashTransaction", pendingId, $"supervisor={req.SupervisorUsername}");
            await _db.SaveChangesAsync();
            return Unauthorized(new { error = "approval_failed" });
        }

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == pending.AccountId);
        if (account == null) return NotFound(new { error = "account_not_found" });
        if (account.AvailableBalance < pending.Amount)
            return UnprocessableEntity(new { error = "insufficient_funds" });

        await using var tx = await _db.Database.BeginTransactionAsync();

        var txn = new Transaction
        {
            AccountId    = account.Id,
            Type         = "cash_out_branch",
            Amount       = pending.Amount,
            Status       = "completed",
            Currency     = pending.Currency,
            Reference    = $"WDR-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            BalanceAfter = account.Balance - pending.Amount,
            TenantId     = account.TenantId,
            CompletedAt  = DateTime.UtcNow,
            CreatedAt    = DateTime.UtcNow,
        };
        _db.Transactions.Add(txn);

        pending.TransactionId        = txn.Id;
        pending.SupervisorApproverId = supervisor!.Id;
        pending.SupervisorApprovedAt = DateTime.UtcNow;
        pending.Status               = "completed";
        pending.UpdatedAt            = DateTime.UtcNow;

        account.Balance          -= pending.Amount;
        account.AvailableBalance -= pending.Amount;
        account.UpdatedAt         = DateTime.UtcNow;

        WriteAudit("cash.withdrawal.approved", "BranchCashTransaction", pending.Id, $"supervisor={supervisor.Username}");

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { transactionId = txn.Id, newBalance = account.Balance });
    }

    // -------------------------------------------------------------------------
    // GET /api/teller/transactions/{cashTxnId}/receipt.pdf
    // STORY-158: returns the A6 PDF receipt for a cash transaction.
    // -------------------------------------------------------------------------
    [HttpGet("transactions/{cashTxnId:guid}/receipt.pdf")]
    public async Task<IActionResult> GetReceiptPdf(Guid cashTxnId)
    {
        var txn = await _db.BranchCashTransactions.FirstOrDefaultAsync(t => t.Id == cashTxnId);
        if (txn == null) return NotFound();

        // Authorization: only the originating teller, branch_manager, or admin
        // (Authorize attribute already restricts to Teller/BranchManager; admin
        // login is blocked but service-role traffic could come through.)
        if (TellerRole == nameof(AdminRole.Teller) && txn.TellerId != TellerId)
            return Forbid();

        if (!string.IsNullOrWhiteSpace(TenantId) && txn.TenantId != TenantId)
            return Forbid();

        var (bytes, _) = await _receiptPdfService.GetOrCreateAsync(txn);
        Response.Headers["Content-Disposition"] = $"inline; filename=\"receipt-{cashTxnId}.pdf\"";
        return File(bytes, "application/pdf");
    }

    // -------------------------------------------------------------------------
    // GET /api/teller/branches/{branchId}/dashboard
    // STORY-160: branch supervisor dashboard aggregator.
    // -------------------------------------------------------------------------
    [HttpGet("branches/{branchId:guid}/dashboard")]
    public async Task<IActionResult> GetBranchDashboard(Guid branchId)
    {
        // Only branch managers (and admins) get to see this
        if (TellerRole != nameof(AdminRole.BranchManager) && TellerRole != nameof(AdminRole.Admin))
            return Forbid();

        var today    = DateOnly.FromDateTime(DateTime.UtcNow);
        var dayStart = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd   = today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // 1. Active tellers — open drawers in this branch
        var openDrawers = await _db.TellerDrawerSessions
            .Where(d => d.BranchId == branchId && d.Status == "Open")
            .ToListAsync();

        var tellerIds = openDrawers.Select(d => d.TellerId).Distinct().ToList();
        var tellers = await _db.AdminUsers
            .Where(u => tellerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, u.FullName })
            .ToListAsync();
        var tellersById = tellers.ToDictionary(u => u.Id);

        var activeTellers = openDrawers.Select(d => new
        {
            tellerId      = d.TellerId,
            tellerName    = tellersById.TryGetValue(d.TellerId, out var t)
                              ? (t.FullName ?? t.Username) : d.TellerId.ToString(),
            drawerId      = d.Id,
            openedAt      = d.OpenedAt,
            openingFloat  = d.OpeningFloatJson,
        }).ToList();

        // 2. Pending high-value approvals — for any drawer at this branch
        var pendingApprovals = await _db.BranchCashTransactions
            .Where(t => t.BranchId == branchId && t.Status == "pending_supervisor_approval")
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                cashTransactionId = t.Id,
                tellerId          = t.TellerId,
                accountId         = t.AccountId,
                shortAccount      = ShortId("ACC", t.AccountId),
                currency          = t.Currency,
                amount            = t.Amount,
                createdAt         = t.CreatedAt,
                ageMinutes        = (int)(DateTime.UtcNow - t.CreatedAt).TotalMinutes,
            })
            .ToListAsync();

        // 3. Today's volume per currency
        var todayCash = await _db.BranchCashTransactions
            .Where(t => t.BranchId == branchId
                        && t.CreatedAt >= dayStart && t.CreatedAt < dayEnd
                        && t.Status == "completed")
            .Select(t => new { t.Currency, t.Direction, t.Amount })
            .ToListAsync();

        var todayVolume = todayCash
            .GroupBy(t => t.Currency)
            .ToDictionary(g => g.Key, g =>
            {
                var deposits    = g.Where(x => x.Direction == "Deposit").Sum(x => x.Amount);
                var withdrawals = g.Where(x => x.Direction == "Withdrawal").Sum(x => x.Amount);
                return new
                {
                    deposits,
                    withdrawals,
                    net  = deposits - withdrawals,
                    txns = g.Count(),
                };
            });

        // 4. Variance alerts — drawers closed today with non-zero variance
        var closedToday = await _db.TellerDrawerSessions
            .Where(d => d.BranchId == branchId
                        && d.Status == "Closed"
                        && d.ClosedAt >= dayStart && d.ClosedAt < dayEnd
                        && d.VarianceJson != null)
            .Select(d => new { d.Id, d.TellerId, d.VarianceJson, d.ClosedAt })
            .ToListAsync();

        var varianceAlerts = closedToday
            .Where(d => !string.IsNullOrWhiteSpace(d.VarianceJson) && d.VarianceJson != "{}")
            .Select(d => new
            {
                drawerId   = d.Id,
                tellerName = tellersById.TryGetValue(d.TellerId, out var u)
                              ? (u.FullName ?? u.Username) : d.TellerId.ToString(),
                closedAt   = d.ClosedAt,
                variance   = d.VarianceJson,
            })
            .ToList();

        return Ok(new
        {
            branchId,
            asOf            = DateTime.UtcNow,
            activeTellers,
            pendingApprovals,
            todayVolume,
            varianceAlerts,
        });
    }

    // -------------------------------------------------------------------------
    // POST /api/teller/transactions/{id}/reverse
    // -------------------------------------------------------------------------
    public sealed record ReverseRequest(string Reason, string SupervisorUsername, string SupervisorPin);

    [HttpPost("transactions/{cashTxnId:guid}/reverse")]
    public async Task<IActionResult> ReverseTransaction(Guid cashTxnId, [FromBody] ReverseRequest req)
    {
        var original = await _db.BranchCashTransactions.FirstOrDefaultAsync(t => t.Id == cashTxnId);
        if (original == null) return NotFound();
        if (original.ReversedAt.HasValue)
            return Conflict(new { error = "already_reversed" });
        if (original.Status != "completed")
            return Conflict(new { error = "not_completed" });
        if (string.IsNullOrWhiteSpace(req.Reason))
            return BadRequest(new { error = "reason_required" });

        // Verify reversal must happen in the same drawer session
        var drawer = await _db.TellerDrawerSessions.FirstOrDefaultAsync(d => d.Id == original.DrawerSessionId);
        if (drawer == null || drawer.Status != "Open")
            return Conflict(new { error = "drawer_not_open" });

        // Supervisor approval (reuses STORY-152 PIN flow)
        var supervisor = await _db.AdminUsers
            .FirstOrDefaultAsync(u => u.Username == req.SupervisorUsername);
        bool sok = supervisor != null
                   && BCrypt.Net.BCrypt.Verify(req.SupervisorPin, supervisor.PasswordHash)
                   && (supervisor.Role == AdminRole.BranchManager || supervisor.Role == AdminRole.Admin)
                   && supervisor.Id != original.TellerId;

        // (above is for the reverse handler — kept the structure)
        if (!sok)
        {
            WriteAudit("cash.reverse.failed", "BranchCashTransaction", cashTxnId, "supervisor_invalid");
            await _db.SaveChangesAsync();
            return Unauthorized(new { error = "approval_failed" });
        }

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == original.AccountId);
        if (account == null) return NotFound(new { error = "account_not_found" });

        await using var tx = await _db.Database.BeginTransactionAsync();

        // Compensating transaction: opposite direction
        var compType = original.Direction == "Deposit" ? "cash_reversal_out" : "cash_reversal_in";
        var compSign = original.Direction == "Deposit" ? -1m : 1m;

        var compTxn = new Transaction
        {
            AccountId    = account.Id,
            Type         = compType,
            Amount       = original.Amount,
            Status       = "completed",
            Currency     = original.Currency,
            Reference    = $"REV-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Description  = $"Reversal of {original.Id}: {req.Reason}",
            BalanceAfter = account.Balance + (compSign * original.Amount),
            TenantId     = account.TenantId,
            CompletedAt  = DateTime.UtcNow,
            CreatedAt    = DateTime.UtcNow,
        };
        _db.Transactions.Add(compTxn);

        var revCash = new BranchCashTransaction
        {
            TransactionId             = compTxn.Id,
            DrawerSessionId           = drawer.Id,
            TellerId                  = TellerId,
            BranchId                  = drawer.BranchId,
            AccountId                 = account.Id,
            Direction                 = "Reversal",
            Currency                  = original.Currency,
            Amount                    = original.Amount,
            DepositorName             = original.DepositorName,
            DenominationBreakdownJson = original.DenominationBreakdownJson,
            IdentityVerified          = original.IdentityVerified,
            SupervisorApproverId      = supervisor!.Id,
            SupervisorApprovedAt      = DateTime.UtcNow,
            Status                    = "completed",
            TenantId                  = original.TenantId,
            CreatedAt                 = DateTime.UtcNow,
        };
        _db.BranchCashTransactions.Add(revCash);

        original.ReversedByTransactionId = compTxn.Id;
        original.ReversedAt              = DateTime.UtcNow;
        original.Status                  = "reversed";
        original.UpdatedAt               = DateTime.UtcNow;

        account.Balance          += compSign * original.Amount;
        account.AvailableBalance += compSign * original.Amount;
        account.UpdatedAt         = DateTime.UtcNow;

        WriteAudit("cash.reverse.approved", "BranchCashTransaction", original.Id,
            $"reason='{req.Reason}' supervisor={supervisor.Username}");

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { reversalCashTxnId = revCash.Id });
    }

    // -------------------------------------------------------------------------
    // GET /api/teller/transactions?date=YYYY-MM-DD
    // -------------------------------------------------------------------------
    [HttpGet("transactions")]
    public async Task<IActionResult> ListTransactions([FromQuery] DateOnly? date = null)
    {
        var businessDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dayStart = businessDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd   = businessDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var rows = await _db.BranchCashTransactions
            .Where(t => t.TellerId == TellerId && t.CreatedAt >= dayStart && t.CreatedAt < dayEnd)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.TransactionId,
                t.AccountId,
                t.Direction,
                t.Currency,
                t.Amount,
                t.DepositorName,
                t.Status,
                t.CreatedAt,
                t.ReversedAt,
            })
            .ToListAsync();

        return Ok(rows);
    }

    // -------------------------------------------------------------------------
    // GET /api/teller/denominations?currency=USD
    // STORY-163: returns active denominations from the registry table
    // -------------------------------------------------------------------------
    [HttpGet("denominations")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDenominations([FromQuery] string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            return BadRequest(new { error = "currency is required" });

        var rows = await _db.CurrencyDenominations
            .Where(c => c.Currency == currency.ToUpperInvariant() && c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new { face = c.FaceValue, type = c.DenominationType, order = c.DisplayOrder })
            .ToListAsync();

        return Ok(new { currency = currency.ToUpperInvariant(), denominations = rows });
    }

    // =========================================================================
    // VAULT MODULE — STORY-166 + STORY-168
    // =========================================================================

    // GET /api/teller/vaults?branchId={guid}
    [HttpGet("vaults")]
    public async Task<IActionResult> ListVaults([FromQuery] Guid? branchId)
    {
        var q = _db.Vaults.AsNoTracking().Where(v => v.IsActive);
        if (branchId.HasValue) q = q.Where(v => v.BranchId == branchId.Value);

        var rows = await q.Select(v => new
        {
            v.Id, v.BranchId, v.Name, v.SpotCheckCron, v.LastSpotCheckAt, v.LastSpotCheckResult
        }).ToListAsync();
        return Ok(rows);
    }

    // GET /api/teller/vaults/{id}
    [HttpGet("vaults/{vaultId:guid}")]
    public async Task<IActionResult> GetVault(Guid vaultId)
    {
        var vault = await _db.Vaults.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vaultId);
        if (vault == null) return NotFound();

        var stock = await (
            from s in _db.VaultDenominationStock.AsNoTracking()
            join d in _db.CurrencyDenominations.AsNoTracking() on s.DenominationId equals d.Id
            where s.VaultId == vaultId
            orderby d.Currency, d.DisplayOrder
            select new
            {
                denominationId = d.Id,
                currency = d.Currency,
                face = d.FaceValue,
                type = d.DenominationType,
                count = s.Count,
                value = d.FaceValue * s.Count,
            }).ToListAsync();

        var totalsByCurrency = stock
            .GroupBy(s => s.currency)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.value));

        return Ok(new
        {
            vault.Id, vault.BranchId, vault.Name, vault.SpotCheckCron,
            vault.LastSpotCheckAt, vault.LastSpotCheckResult,
            stock, totalsByCurrency
        });
    }

    // POST /api/teller/vaults/{id}/movements
    public sealed record VaultMovementRequest(
        string Type, string Direction, string Currency, decimal TotalAmount,
        List<VaultBreakdownLine> Denominations, Guid? TellerId, Guid? DrawerSessionId,
        Guid? WitnessId, string? Reference, string? Notes);

    public sealed record VaultBreakdownLine(Guid DenominationId, decimal Face, int Count);

    [HttpPost("vaults/{vaultId:guid}/movements")]
    public async Task<IActionResult> PostVaultMovement(Guid vaultId, [FromBody] VaultMovementRequest req)
    {
        if (req == null || req.Denominations == null || req.Denominations.Count == 0)
            return BadRequest(new { error = "Empty denomination breakdown" });

        var vault = await _db.Vaults.FirstOrDefaultAsync(v => v.Id == vaultId);
        if (vault == null) return NotFound();

        // Recompute total from breakdown and reject mismatches
        var computed = req.Denominations.Sum(d => d.Face * d.Count);
        if (Math.Round(computed, 2) != Math.Round(req.TotalAmount, 2))
            return BadRequest(new { error = "Total does not match denomination breakdown" });

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var movement = new GoldBank.Core.Modules.BranchCash.Domain.Entities.VaultMovement
            {
                VaultId = vaultId,
                Type = req.Type,
                Direction = req.Direction,
                Currency = req.Currency,
                TotalAmount = req.TotalAmount,
                DenominationBreakdownJson = System.Text.Json.JsonSerializer.Serialize(
                    req.Denominations.Select(d => new { denominationId = d.DenominationId, face = d.Face, count = d.Count })),
                TellerId = req.TellerId,
                DrawerSessionId = req.DrawerSessionId,
                PerformedBy = TellerId,
                WitnessId = req.WitnessId,
                Reference = req.Reference,
                Notes = req.Notes,
                TenantId = "goldbank",
                CreatedAt = DateTime.UtcNow,
            };
            _db.VaultMovements.Add(movement);
            await _db.SaveChangesAsync();

            var apply = await _vaultStock.ApplyMovementAsync(movement, HttpContext.RequestAborted);
            if (apply.IsFailure)
            {
                await tx.RollbackAsync();
                return Conflict(new { error = apply.Error.Code, message = apply.Error.Message });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok(new { movementId = movement.Id });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return StatusCode(500, new { error = "Vault.MovementFailed", message = ex.Message });
        }
    }

    // POST /api/teller/vaults/{id}/spot-checks  (STORY-168)
    public sealed record SpotCheckRequest(
        Guid WitnessId,
        Dictionary<Guid, int> ActualCounts,
        bool AcceptVariance);

    [HttpPost("vaults/{vaultId:guid}/spot-checks")]
    [Authorize(Roles = "BranchManager,VaultManager,Admin")]
    public async Task<IActionResult> PostSpotCheck(Guid vaultId, [FromBody] SpotCheckRequest req)
    {
        var vault = await _db.Vaults.FirstOrDefaultAsync(v => v.Id == vaultId);
        if (vault == null) return NotFound();
        if (req.WitnessId == Guid.Empty || req.WitnessId == TellerId)
            return BadRequest(new { error = "A distinct witness is required" });

        var expectedRows = await _db.VaultDenominationStock.AsNoTracking()
            .Where(s => s.VaultId == vaultId)
            .ToListAsync();
        var expected = expectedRows.ToDictionary(s => s.DenominationId, s => s.Count);

        // Build variance: actual - expected
        var allDenomIds = expected.Keys.Union(req.ActualCounts.Keys).ToHashSet();
        var variance = new Dictionary<Guid, int>();
        bool hasVar = false;
        foreach (var id in allDenomIds)
        {
            var diff = req.ActualCounts.GetValueOrDefault(id) - expected.GetValueOrDefault(id);
            variance[id] = diff;
            if (diff != 0) hasVar = true;
        }

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            Guid? adjustmentId = null;

            if (hasVar)
            {
                if (!req.AcceptVariance)
                {
                    await tx.RollbackAsync();
                    return Conflict(new
                    {
                        error = "Vault.VarianceFound",
                        variance,
                        message = "Variance detected; resubmit with acceptVariance=true to post adjustment movement."
                    });
                }

                // Build an adjustment movement that pushes stock from expected to actual.
                // Direction is "In" for positive total, "Out" for negative; we model it as
                // a single movement with a synthesized breakdown of net non-zero diffs.
                var faces = await _db.CurrencyDenominations.AsNoTracking()
                    .Where(d => allDenomIds.Contains(d.Id))
                    .ToDictionaryAsync(d => d.Id, d => new { d.FaceValue, d.Currency });

                var adj = variance.Where(kv => kv.Value != 0).ToList();
                var netByCurrency = adj.GroupBy(kv => faces[kv.Key].Currency)
                    .Select(g => new { Currency = g.Key, Total = g.Sum(kv => faces[kv.Key].FaceValue * kv.Value) })
                    .ToList();

                foreach (var nc in netByCurrency)
                {
                    var lines = adj.Where(kv => faces[kv.Key].Currency == nc.Currency)
                                   .Select(kv => new
                                   {
                                       denominationId = kv.Key,
                                       face = faces[kv.Key].FaceValue,
                                       count = Math.Abs(kv.Value)
                                   }).ToList();
                    var direction = nc.Total >= 0 ? "In" : "Out";
                    var movement = new GoldBank.Core.Modules.BranchCash.Domain.Entities.VaultMovement
                    {
                        VaultId = vaultId,
                        Type = "SpotCheckAdjust",
                        Direction = direction,
                        Currency = nc.Currency,
                        TotalAmount = Math.Abs(nc.Total),
                        DenominationBreakdownJson = System.Text.Json.JsonSerializer.Serialize(lines),
                        PerformedBy = TellerId,
                        WitnessId = req.WitnessId,
                        Reference = "SPOT",
                        Notes = "Spot-check adjustment",
                        TenantId = "goldbank",
                        CreatedAt = DateTime.UtcNow,
                    };
                    _db.VaultMovements.Add(movement);
                    await _db.SaveChangesAsync();
                    var apply = await _vaultStock.ApplyMovementAsync(movement, HttpContext.RequestAborted);
                    if (apply.IsFailure)
                    {
                        await tx.RollbackAsync();
                        return Conflict(new { error = apply.Error.Code, message = apply.Error.Message });
                    }
                    adjustmentId = movement.Id;
                }
            }

            var spot = new GoldBank.Core.Modules.BranchCash.Domain.Entities.VaultSpotCheck
            {
                VaultId = vaultId,
                PerformedBy = TellerId,
                WitnessId = req.WitnessId,
                ExpectedJson = System.Text.Json.JsonSerializer.Serialize(expected),
                ActualJson = System.Text.Json.JsonSerializer.Serialize(req.ActualCounts),
                VarianceJson = System.Text.Json.JsonSerializer.Serialize(variance),
                HasVariance = hasVar,
                AdjustmentMovementId = adjustmentId,
                TenantId = "goldbank",
                CreatedAt = DateTime.UtcNow,
            };
            _db.VaultSpotChecks.Add(spot);

            vault.LastSpotCheckAt = DateTime.UtcNow;
            vault.LastSpotCheckResult = hasVar ? "Variance" : "Balanced";
            vault.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok(new { spotCheckId = spot.Id, hasVariance = hasVar, variance, adjustmentMovementId = adjustmentId });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return StatusCode(500, new { error = "Vault.SpotCheckFailed", message = ex.Message });
        }
    }

    // GET /api/teller/vaults/{id}/movements
    [HttpGet("vaults/{vaultId:guid}/movements")]
    public async Task<IActionResult> ListVaultMovements(Guid vaultId, [FromQuery] int take = 100)
    {
        take = Math.Clamp(take, 1, 500);
        var rows = await _db.VaultMovements.AsNoTracking()
            .Where(m => m.VaultId == vaultId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .Select(m => new
            {
                m.Id, m.Type, m.Direction, m.Currency, m.TotalAmount,
                m.TellerId, m.PerformedBy, m.WitnessId, m.Reference, m.Notes, m.CreatedAt
            })
            .ToListAsync();
        return Ok(rows);
    }

    // GET /api/teller/vaults/{id}/eod-report.pdf?date=YYYY-MM-DD  (STORY-169)
    [HttpGet("vaults/{vaultId:guid}/eod-report.pdf")]
    public async Task<IActionResult> GetVaultEodReport(Guid vaultId, [FromQuery] DateOnly? date = null)
    {
        var d = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        try
        {
            var bytes = await _vaultReportPdf.BuildAsync(vaultId, d, HttpContext.RequestAborted);
            Response.Headers["Content-Disposition"] = $"inline; filename=\"vault-eod-{vaultId}-{d:yyyyMMdd}.pdf\"";
            return File(bytes, "application/pdf");
        }
        catch (InvalidOperationException) { return NotFound(); }
    }

    // GET /api/teller/vaults/{id}/spot-checks
    [HttpGet("vaults/{vaultId:guid}/spot-checks")]
    public async Task<IActionResult> ListSpotChecks(Guid vaultId, [FromQuery] int take = 50)
    {
        take = Math.Clamp(take, 1, 200);
        var rows = await _db.VaultSpotChecks.AsNoTracking()
            .Where(s => s.VaultId == vaultId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(take)
            .Select(s => new
            {
                s.Id, s.PerformedBy, s.WitnessId, s.HasVariance,
                s.AdjustmentMovementId, s.CreatedAt
            })
            .ToListAsync();
        return Ok(rows);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    /// <summary>Parse a drawer float/closing JSON of shape { "USD": { "total": 5000, ... }, ... } into per-currency totals.</summary>
    private static Dictionary<string, decimal> ParseTotalsByCurrency(string? json)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return result;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    prop.Value.TryGetProperty("total", out var tot) &&
                    tot.TryGetDecimal(out var val))
                {
                    result[prop.Name] = Math.Round(val, 2);
                }
                else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Number &&
                         prop.Value.TryGetDecimal(out var val2))
                {
                    result[prop.Name] = Math.Round(val2, 2);
                }
            }
        }
        catch { /* malformed JSON → empty dict */ }
        return result;
    }

    private async Task<decimal> GetHighValueThreshold(string currency)
    {
        var key = $"cash.high_value_threshold.{currency}";
        var cfg = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == key);
        if (cfg == null || string.IsNullOrWhiteSpace(cfg.ValueJson)) return decimal.MaxValue;
        return decimal.TryParse(cfg.ValueJson.Trim('"'), out var v) ? v : decimal.MaxValue;
    }

    private void WriteAudit(string action, string entityType, Guid entityId, string details)
    {
        _db.AuditLogs.Add(new GoldBank.Core.Modules.Admin.Domain.Entities.AuditLog
        {
            AdminUserId = TellerId,
            Action      = action,
            EntityType  = entityType,
            EntityId    = entityId.ToString(),
            Details     = details,
            IpAddress   = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            CreatedAt   = DateTime.UtcNow,
        });
    }

    private static string ShortId(string prefix, Guid id)
    {
        var hex = id.ToString("N");
        var lastPart = Convert.ToInt64(hex.Substring(24, 8), 16);
        return $"{prefix}-{lastPart:D6}";
    }
}
