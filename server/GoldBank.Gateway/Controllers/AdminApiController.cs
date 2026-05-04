using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.AssetCustody.Domain.Entities;

namespace GoldBank.Gateway.Controllers;

/// <summary>
/// REST API controller providing admin data endpoints for the bank-client React app.
/// Queries GoldBankDbContext directly — no business logic, just DB reads.
/// </summary>
[ApiController]
[Route("api/admin")]
[EnableCors("BankClient")]
public class AdminApiController : ControllerBase
{
    private readonly GoldBankDbContext _db;

    public AdminApiController(GoldBankDbContext db)
    {
        _db = db;
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/dashboard
    // -------------------------------------------------------------------------
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var totalUsers       = await _db.Accounts.CountAsync();
        var activeUsers      = await _db.Accounts.CountAsync(a => a.Status == "active");
        var totalTx          = await _db.Transactions.CountAsync();
        var txVolume         = await _db.Transactions.SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var totalMerchants   = await _db.Merchants.CountAsync();
        var totalAgentFloats = await _db.AgentFloats.CountAsync();
        var totalLoans       = await _db.Loans.CountAsync();

        // Daily transaction counts for the last 30 days
        var cutoff = DateTime.UtcNow.AddDays(-30).Date;
        var dailyRaw = await _db.Transactions
            .Where(t => t.CreatedAt >= cutoff)
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();

        var daily = dailyRaw.Select(x => new
        {
            date  = x.Date.ToString("MMM dd"),
            count = x.Count,
        });

        return Ok(new
        {
            totalUsers,
            activeUsers,
            totalTransactions = totalTx,
            transactionVolume = txVolume,
            merchants  = totalMerchants,
            agents     = totalAgentFloats,
            loans      = totalLoans,
            dailyTransactions = daily,
        });
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/customers?page=0&pageSize=10&search=&status=
    // Groups dual-currency accounts (ZWG + USD) into a single customer row.
    // -------------------------------------------------------------------------
    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] int    page       = 0,
        [FromQuery] int    pageSize   = 10,
        [FromQuery] string search     = "",
        [FromQuery] string status     = "")
    {
        var query = _db.Accounts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a =>
                (a.FirstName != null && a.FirstName.Contains(search)) ||
                (a.LastName  != null && a.LastName.Contains(search))  ||
                a.PhoneNumber.Contains(search)                         ||
                (a.Email     != null && a.Email.Contains(search)));

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);

        // Group by phone to merge ZWG + USD accounts into one customer row
        var grouped = await query.ToListAsync();

        var customers = grouped
            .GroupBy(a => a.PhoneNumber)
            .Select(g =>
            {
                var primary = g.OrderBy(a => a.CreatedAt).First();
                return new
                {
                    id         = ShortId("ACC", primary.Id),
                    name       = (primary.FirstName + " " + primary.LastName).Trim(),
                    phone      = primary.PhoneCountryCode + primary.PhoneNumber,
                    email      = primary.Email,
                    status     = MapStatus(primary.Status),
                    kycLevel   = primary.KycLevel,
                    balanceZwg = g.FirstOrDefault(a => a.Currency == "ZWG")?.Balance,
                    balanceUsd = g.FirstOrDefault(a => a.Currency == "USD")?.Balance,
                    nationalId = primary.NationalId,
                    created    = primary.CreatedAt.ToString("yyyy-MM-dd"),
                    lastLogin  = primary.LastLoginAt.HasValue
                        ? primary.LastLoginAt.Value.ToString("yyyy-MM-dd HH:mm")
                        : (string?)null,
                };
            })
            .OrderByDescending(c => c.created)
            .ToList();

        var total = customers.Count;
        var items = customers.Skip(page * pageSize).Take(pageSize).ToList();

        return Ok(new { items, total });
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/transactions?page=0&pageSize=50&type=&status=&accountId=
    // -------------------------------------------------------------------------
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int    page      = 0,
        [FromQuery] int    pageSize  = 50,
        [FromQuery] string type      = "",
        [FromQuery] string status    = "",
        [FromQuery] string accountId = "")
    {
        var query = _db.Transactions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(t => t.Type == type);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);

        if (Guid.TryParse(accountId, out var accGuid))
            query = query.Where(t => t.AccountId == accGuid);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                id           = ShortId("TXN", t.Id),
                accountId    = ShortId("ACC", t.AccountId),
                type         = t.Type,
                amount       = t.Amount,
                fee          = t.Fee,
                status       = MapStatus(t.Status),
                reference    = t.Reference,
                description  = t.Description,
                counterparty = t.CounterpartyName ?? t.CounterpartyPhone,
                currency     = t.Currency,
                date         = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                completedAt  = t.CompletedAt.HasValue
                    ? t.CompletedAt.Value.ToString("yyyy-MM-dd HH:mm")
                    : null,
            })
            .ToListAsync();

        return Ok(new { items, total });
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/disputes?status=
    // -------------------------------------------------------------------------
    [HttpGet("disputes")]
    public async Task<IActionResult> GetDisputes([FromQuery] string status = "")
    {
        var query = _db.Disputes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            // DisputeStatus enum: Open=0, Investigating=1, Resolved=2, Rejected=3
            if (Enum.TryParse<GoldBank.Core.Modules.Admin.Domain.Entities.DisputeStatus>(
                    status, ignoreCase: true, out var ds))
                query = query.Where(d => d.Status == ds);
        }

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                id            = ShortId("DSP", d.Id),
                transactionId = ShortId("TXN", d.TransactionId),
                accountId     = ShortId("ACC", d.AccountId),
                type          = d.Type.ToString(),
                description   = d.Description,
                status        = MapStatus(d.Status.ToString()),
                resolution    = d.Resolution,
                refundAmount  = d.RefundAmount,
                refundCurrency= d.RefundCurrency,
                agent         = d.AdminUserId.HasValue ? ShortId("AGT", d.AdminUserId.Value) : "",
                filed         = d.CreatedAt.ToString("yyyy-MM-dd"),
                slaHours      = (int)(DateTime.UtcNow - d.CreatedAt).TotalHours,
                resolved      = d.ResolvedAt.HasValue
                    ? d.ResolvedAt.Value.ToString("yyyy-MM-dd HH:mm")
                    : "",
            })
            .ToListAsync();

        return Ok(items);
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/disputes/{shortId}/activities
    // Returns the activity timeline for a single dispute (parsed from jsonb).
    // -------------------------------------------------------------------------
    [HttpGet("disputes/{shortId}/activities")]
    public async Task<IActionResult> GetDisputeActivities(string shortId)
    {
        var allDisputes = await _db.Disputes.Select(d => new { d.Id, d.ActivitiesJson }).ToListAsync();
        var match = allDisputes.FirstOrDefault(d => ShortId("DSP", d.Id) == shortId);
        if (match == null) return NotFound();

        if (string.IsNullOrWhiteSpace(match.ActivitiesJson))
            return Ok(Array.Empty<object>());

        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<DisputeActivityEntry>>(match.ActivitiesJson)
                         ?? new List<DisputeActivityEntry>();
            var ordered = parsed.OrderByDescending(e => e.Timestamp).ToList();
            return Ok(ordered);
        }
        catch
        {
            return Ok(Array.Empty<object>());
        }
    }

    // -------------------------------------------------------------------------
    // POST /api/admin/disputes/{shortId}/activities
    // Appends a new activity entry to the dispute timeline.
    // Body: { actionType, notes, agent }
    // -------------------------------------------------------------------------
    [HttpPost("disputes/{shortId}/activities")]
    public async Task<IActionResult> AddDisputeActivity(string shortId, [FromBody] DisputeActivityEntry body)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.ActionType))
            return BadRequest("actionType is required");

        // Project only needed columns to avoid loading the full Dispute entity
        // (existing rows may have legacy enum values that don't deserialize)
        var allRows = await _db.Disputes
            .Select(d => new { d.Id, d.ActivitiesJson })
            .ToListAsync();
        var match = allRows.FirstOrDefault(d => ShortId("DSP", d.Id) == shortId);
        if (match == null) return NotFound();

        var existing = string.IsNullOrWhiteSpace(match.ActivitiesJson)
            ? new List<DisputeActivityEntry>()
            : (System.Text.Json.JsonSerializer.Deserialize<List<DisputeActivityEntry>>(match.ActivitiesJson)
               ?? new List<DisputeActivityEntry>());

        existing.Add(new DisputeActivityEntry
        {
            Timestamp  = DateTime.UtcNow,
            ActionType = body.ActionType,
            Notes      = body.Notes ?? "",
            Agent      = string.IsNullOrWhiteSpace(body.Agent) ? "admin" : body.Agent,
        });

        var newJson = System.Text.Json.JsonSerializer.Serialize(existing);
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE bank.disputes SET activities_json = {newJson}::jsonb, updated_at = NOW() WHERE \"Id\" = {match.Id}");

        return Ok(new { ok = true, count = existing.Count });
    }

    public sealed class DisputeActivityEntry
    {
        [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("agent")]
        public string Agent { get; set; } = "admin";

        [System.Text.Json.Serialization.JsonPropertyName("actionType")]
        public string ActionType { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("notes")]
        public string Notes { get; set; } = "";
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/fraud-alerts/{shortId}/activities
    // -------------------------------------------------------------------------
    [HttpGet("fraud-alerts/{shortId}/activities")]
    public async Task<IActionResult> GetFraudAlertActivities(string shortId)
    {
        var rows = await _db.FraudAlerts
            .Select(f => new { f.Id, f.ActivitiesJson })
            .ToListAsync();
        var match = rows.FirstOrDefault(r => ShortId("FRD", r.Id) == shortId);
        if (match == null) return NotFound();

        if (string.IsNullOrWhiteSpace(match.ActivitiesJson))
            return Ok(Array.Empty<object>());

        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<DisputeActivityEntry>>(match.ActivitiesJson)
                         ?? new List<DisputeActivityEntry>();
            return Ok(parsed.OrderByDescending(e => e.Timestamp).ToList());
        }
        catch
        {
            return Ok(Array.Empty<object>());
        }
    }

    // -------------------------------------------------------------------------
    // POST /api/admin/fraud-alerts/{shortId}/activities
    // -------------------------------------------------------------------------
    [HttpPost("fraud-alerts/{shortId}/activities")]
    public async Task<IActionResult> AddFraudAlertActivity(string shortId, [FromBody] DisputeActivityEntry body)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.ActionType))
            return BadRequest("actionType is required");

        var rows = await _db.FraudAlerts
            .Select(f => new { f.Id, f.ActivitiesJson })
            .ToListAsync();
        var match = rows.FirstOrDefault(r => ShortId("FRD", r.Id) == shortId);
        if (match == null) return NotFound();

        var existing = string.IsNullOrWhiteSpace(match.ActivitiesJson)
            ? new List<DisputeActivityEntry>()
            : (System.Text.Json.JsonSerializer.Deserialize<List<DisputeActivityEntry>>(match.ActivitiesJson)
               ?? new List<DisputeActivityEntry>());

        existing.Add(new DisputeActivityEntry
        {
            Timestamp  = DateTime.UtcNow,
            ActionType = body.ActionType,
            Notes      = body.Notes ?? "",
            Agent      = string.IsNullOrWhiteSpace(body.Agent) ? "admin" : body.Agent,
        });

        var newJson = System.Text.Json.JsonSerializer.Serialize(existing);
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE bank.fraud_alerts SET activities_json = {newJson}::jsonb, updated_at = NOW() WHERE \"Id\" = {match.Id}");

        return Ok(new { ok = true, count = existing.Count });
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/fraud-alerts?status=&severity=
    // -------------------------------------------------------------------------
    [HttpGet("fraud-alerts")]
    public async Task<IActionResult> GetFraudAlerts(
        [FromQuery] string status   = "",
        [FromQuery] string severity = "")
    {
        var query = _db.FraudAlerts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(f => f.Status == status);

        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(f => f.Severity == severity);

        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                id            = ShortId("FRD", f.Id),
                accountId     = ShortId("ACC", f.AccountId),
                transactionId = ShortId("TXN", f.TransactionId),
                type          = f.AlertType,
                severity      = f.Severity,
                description   = f.Description,
                status        = MapStatus(f.Status),
                adminNotes    = f.AdminNotes,
                reviewedAt    = f.ReviewedAt.HasValue
                    ? f.ReviewedAt.Value.ToString("yyyy-MM-dd HH:mm")
                    : null,
                reviewedBy    = f.ReviewedBy,
                created       = f.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            })
            .ToListAsync();

        return Ok(items);
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/kyc-queue?status=
    // -------------------------------------------------------------------------
    [HttpGet("kyc-queue")]
    public async Task<IActionResult> GetKycQueue([FromQuery] string status = "")
    {
        // Only show ID-type documents in the review queue (not selfies, which are
        // matched up to their parent ID via account_id below).
        var idDocTypes = new[] { "national_id", "passport", "drivers_license" };

        var query = _db.KycDocuments
            .Where(k => idDocTypes.Contains(k.DocumentType));

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(k => k.Status == status.ToLowerInvariant());

        var rows = await query
            .Join(_db.Accounts, k => k.AccountId, a => a.Id, (k, a) => new { k, a })
            .OrderByDescending(x => x.k.CreatedAt)
            .Select(x => new
            {
                x.k,
                x.a,
                // Latest selfie document for this account (if any)
                selfie = _db.KycDocuments
                    .Where(s => s.AccountId == x.k.AccountId && s.DocumentType == "selfie")
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => new { s.FileData, s.ContentType })
                    .FirstOrDefault(),
                // Latest KycVerification (AI) for this account
                v = _db.KycVerifications
                    .Where(vv => vv.AccountId == x.k.AccountId)
                    .OrderByDescending(vv => vv.CreatedAt)
                    .FirstOrDefault(),
            })
            .ToListAsync();

        static string MimeOrJpeg(string? ct) => string.IsNullOrWhiteSpace(ct) ? "image/jpeg" : ct;

        var items = rows.Select(x => new
        {
            id              = ShortId("KYC", x.k.Id),
            accountId       = ShortId("ACC", x.k.AccountId),
            name            = (x.a.FirstName + " " + x.a.LastName).Trim(),
            documentType    = x.k.DocumentType,
            status          = MapStatus(x.k.Status),
            submittedDate   = x.k.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            level           = x.a.KycLevel,
            faceMatchScore  = x.v?.FaceMatchScore ?? 0.85,
            aiDecision      = x.v?.OverallDecision
                                ?? (x.k.Status == "approved" ? "AutoApproved"
                                    : x.k.Status == "rejected" ? "Rejected"
                                    : "Pending"),
            aiReason        = x.v?.RejectionReason ?? "",
            extractedName   = x.v?.ExtractedFullName ?? "",
            extractedIdNumber = x.v?.ExtractedIdNumber ?? "",
            extractedDob    = x.v?.ExtractedDateOfBirth?.ToString("yyyy-MM-dd") ?? "",
            nameMatch       = x.v?.NameMatch ?? true,
            idMatch         = x.v?.IdNumberMatch ?? true,
            dobMatch        = x.v?.DobMatch ?? (x.k.Status != "rejected"),
            // Prefer raw file_data on the kyc_documents row; fall back to kyc_verifications bytea.
            idImageUrl      = x.k.FileData != null
                                ? $"data:{MimeOrJpeg(x.k.ContentType)};base64," + Convert.ToBase64String(x.k.FileData)
                                : x.v?.IdDocumentImageData != null
                                    ? "data:image/jpeg;base64," + Convert.ToBase64String(x.v.IdDocumentImageData)
                                    : null,
            selfieImageUrl  = x.selfie?.FileData != null
                                ? $"data:{MimeOrJpeg(x.selfie.ContentType)};base64," + Convert.ToBase64String(x.selfie.FileData)
                                : x.v?.SelfieImageData != null
                                    ? "data:image/jpeg;base64," + Convert.ToBase64String(x.v.SelfieImageData)
                                    : null,
        }).ToList();

        return Ok(items);
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/loans?status=&page=0&pageSize=20
    // -------------------------------------------------------------------------
    [HttpGet("loans")]
    public async Task<IActionResult> GetLoans(
        [FromQuery] string status   = "",
        [FromQuery] int    page     = 0,
        [FromQuery] int    pageSize = 20)
    {
        var query = _db.Loans.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(l => l.Status == status);

        var total = await query.CountAsync();

        var items = await query
            .Join(_db.Accounts, l => l.AccountId, a => a.Id, (l, a) => new { l, a })
            .OrderByDescending(x => x.l.CreatedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                id                 = ShortId("LOAN", x.l.Id),
                accountId          = ShortId("ACC", x.l.AccountId),
                name               = (x.a.FirstName + " " + x.a.LastName).Trim(),
                phone              = x.a.PhoneCountryCode + x.a.PhoneNumber,
                email              = x.a.Email,
                kycLevel           = x.a.KycLevel,
                reference          = x.l.Reference,
                amount             = x.l.Principal,
                outstandingBalance = x.l.OutstandingBalance,
                interestRate       = x.l.InterestRate,
                tenure             = x.l.TenureMonths,
                monthlyRepayment   = x.l.MonthlyPayment,
                purpose            = x.l.Purpose,
                status             = MapStatus(x.l.Status),
                creditScore        = x.l.CreditScore,
                paymentsMade       = x.l.PaymentsMade,
                currency           = x.l.Currency,
                appliedDate        = x.l.CreatedAt.ToString("yyyy-MM-dd"),
                verificationStatus = "Not Available",
            })
            .ToListAsync();

        return Ok(new { items, total });
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/merchants?status=&page=0&pageSize=20
    // -------------------------------------------------------------------------
    [HttpGet("merchants")]
    public async Task<IActionResult> GetMerchants(
        [FromQuery] string status   = "",
        [FromQuery] int    page     = 0,
        [FromQuery] int    pageSize = 20)
    {
        var query = _db.Merchants.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(m => m.Status == status);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                id                 = m.Id,
                merchantCode       = m.MerchantCode,
                ownerAccountId     = m.OwnerAccountId,
                businessName       = m.BusinessName,
                businessType       = m.BusinessType,
                registrationNumber = m.RegistrationNumber,
                taxId              = m.TaxId,
                categoryCode       = m.CategoryCode,
                businessAddress    = m.BusinessAddress,
                isAgent            = m.IsAgent,
                status             = MapStatus(m.Status),
                kycStatus          = m.KycStatus,
                created            = m.CreatedAt.ToString("yyyy-MM-dd"),
                activatedAt        = m.ActivatedAt.HasValue
                    ? m.ActivatedAt.Value.ToString("yyyy-MM-dd")
                    : null,
            })
            .ToListAsync();

        return Ok(new { items, total });
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/admin-users
    // -------------------------------------------------------------------------
    [HttpGet("admin-users")]
    public async Task<IActionResult> GetAdminUsers()
    {
        var items = await _db.AdminUsers
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                id          = u.Id,
                username    = u.Username,
                fullName    = u.FullName,
                email       = u.Email,
                role        = u.Role.ToString(),
                branchId    = u.BranchId,
                tenantId    = u.TenantId,
                isActive    = u.IsActive,
                lastLoginAt = u.LastLoginAt.HasValue
                    ? u.LastLoginAt.Value.ToString("yyyy-MM-dd HH:mm")
                    : null,
            })
            .ToListAsync();

        return Ok(items);
    }

    // -------------------------------------------------------------------------
    // POST /api/admin/admin-users — create a new admin user
    // -------------------------------------------------------------------------
    public sealed class CreateAdminUserRequest
    {
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email    { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role     { get; set; } = "";
        public Guid?  BranchId { get; set; }
        public string? TenantId { get; set; }
    }

    [HttpPost("admin-users")]
    public async Task<IActionResult> CreateAdminUser([FromBody] CreateAdminUserRequest body)
    {
        if (body == null) return BadRequest(new { error = "missing_body" });
        if (string.IsNullOrWhiteSpace(body.Username)) return BadRequest(new { error = "username_required" });
        if (string.IsNullOrWhiteSpace(body.Password)) return BadRequest(new { error = "password_required" });
        if (!Enum.TryParse<GoldBank.Core.Modules.Admin.Domain.Entities.AdminRole>(body.Role, out var roleEnum))
            return BadRequest(new { error = "invalid_role" });

        var dup = await _db.AdminUsers.AnyAsync(u => u.Username == body.Username);
        if (dup) return Conflict(new { error = "username_taken" });

        var user = new GoldBank.Core.Modules.Admin.Domain.Entities.AdminUser
        {
            Username     = body.Username,
            FullName     = body.FullName,
            Email        = body.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(body.Password, 11),
            Role         = roleEnum,
            BranchId     = body.BranchId,
            TenantId     = body.TenantId ?? "goldbank",
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow,
        };
        _db.AdminUsers.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new { id = user.Id, username = user.Username });
    }

    // -------------------------------------------------------------------------
    // PUT /api/admin/admin-users/{id} — update an existing admin user
    // -------------------------------------------------------------------------
    public sealed class UpdateAdminUserRequest
    {
        public string? FullName { get; set; }
        public string? Email    { get; set; }
        public string? Password { get; set; }   // optional — only updated if non-empty
        public string? Role     { get; set; }
        public Guid?   BranchId { get; set; }
        public bool?   IsActive { get; set; }
    }

    [HttpPut("admin-users/{id:guid}")]
    public async Task<IActionResult> UpdateAdminUser(Guid id, [FromBody] UpdateAdminUserRequest body)
    {
        if (body == null) return BadRequest(new { error = "missing_body" });

        var user = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(body.FullName)) user.FullName = body.FullName;
        if (!string.IsNullOrWhiteSpace(body.Email))    user.Email    = body.Email;
        if (body.BranchId.HasValue)                    user.BranchId = body.BranchId;
        if (body.IsActive.HasValue)                    user.IsActive = body.IsActive.Value;

        if (!string.IsNullOrWhiteSpace(body.Role))
        {
            if (!Enum.TryParse<GoldBank.Core.Modules.Admin.Domain.Entities.AdminRole>(body.Role, out var roleEnum))
                return BadRequest(new { error = "invalid_role" });
            user.Role = roleEnum;
        }

        if (!string.IsNullOrWhiteSpace(body.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(body.Password, 11);

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { id = user.Id, username = user.Username });
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/branches
    // -------------------------------------------------------------------------
    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches()
    {
        var items = await _db.Branches
            .OrderBy(b => b.Name)
            .Select(b => new
            {
                id        = b.Id,
                name      = b.Name,
                code      = b.Code,
                address   = b.Address,
                city      = b.City,
                phone     = b.Phone,
                tenantId  = b.TenantId,
                isActive  = b.IsActive,
                createdAt = b.CreatedAt.ToString("yyyy-MM-dd"),
            })
            .ToListAsync();

        return Ok(items);
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/assets
    // Customer-scoped assets held in custody. Joins Asset → Customer (for the
    // owner's name + phone) and Asset → DepositHouse (for the storage facility).
    // Maps the C# enums to the display strings the AssetValuation.jsx page uses
    // ("Gold Coin" rather than "GoldCoin", "Pending Review" rather than
    // "PendingVerification", etc.) so the existing filter dropdowns keep working.
    // -------------------------------------------------------------------------
    [HttpGet("assets")]
    public async Task<IActionResult> GetAssets()
    {
        // Pull rows; keep the projection server-side and shape on the client side
        // so the enum-to-display mapping doesn't need to be expressed in EF SQL.
        var rows = await (
            from a in _db.Assets
            where !a.IsDeleted
            join c in _db.Customers on a.CustomerId equals c.Id into cs
            from c in cs.DefaultIfEmpty()
            join h in _db.DepositHouses on a.DepositHouseId equals h.Id into hs
            from h in hs.DefaultIfEmpty()
            // Currency comes from the most recent valuation if any; otherwise default to USD.
            let lastValuation = _db.AssetValuations
                .Where(v => v.AssetId == a.Id)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefault()
            select new
            {
                a.Id,
                CustomerFirstName = c != null ? c.FirstName : null,
                CustomerLastName  = c != null ? c.LastName  : null,
                CustomerPhone     = c != null ? c.PhoneNumber : null,
                a.AssetType,
                a.Description,
                a.Quantity,
                a.Unit,
                DepositHouseName  = h != null ? h.Name : null,
                a.LastValuationAmount,
                ValuationCurrency = lastValuation != null ? lastValuation.Currency : "USD",
                a.Status,
                a.VerificationStatus,
                a.CreatedAt,
                a.LastValuationDate,
            }).ToListAsync();

        var items = rows.Select(r => new
        {
            id              = ShortId("AST", r.Id),
            assetUuid       = r.Id, // full GUID — clients echo this back on POST /asset-valuations
            customer        = (string.IsNullOrWhiteSpace(r.CustomerFirstName) && string.IsNullOrWhiteSpace(r.CustomerLastName))
                                ? r.CustomerPhone ?? "Unknown"
                                : $"{r.CustomerFirstName} {r.CustomerLastName}".Trim(),
            type            = AssetTypeDisplay(r.AssetType),
            description     = r.Description,
            quantity        = r.Quantity,
            unit            = r.Unit,
            depositHouse    = r.DepositHouseName ?? "—",
            currentValue    = r.LastValuationAmount,
            currency        = r.ValuationCurrency ?? "USD",
            status          = AssetStatusDisplay(r.Status),
            verification    = VerificationStatusDisplay(r.VerificationStatus),
            registeredDate  = r.CreatedAt.ToString("yyyy-MM-dd"),
            lastValued      = r.LastValuationDate?.ToString("yyyy-MM-dd"),
            assignedValuer  = (string?)null, // not modelled yet
        }).ToList();

        return Ok(items);
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/asset-valuations
    // Backing data for the Valuation History tab. One row per AssetValuation,
    // joined to the Asset and Customer for display. "prevValue" is the previous
    // valuation on the same asset (so the page can compute % change).
    // -------------------------------------------------------------------------
    [HttpGet("asset-valuations")]
    public async Task<IActionResult> GetAssetValuations()
    {
        var rows = await (
            from v in _db.AssetValuations
            join a in _db.Assets on v.AssetId equals a.Id
            join c in _db.Customers on a.CustomerId equals c.Id into cs
            from c in cs.DefaultIfEmpty()
            orderby v.CreatedAt descending
            select new
            {
                v.Id,
                v.AssetId,
                v.ValuationAmount,
                v.Currency,
                v.ValuerName,
                v.ValuerLicense,
                v.Notes,
                v.CreatedAt,
                AssetDescription  = a.Description,
                CustomerFirstName = c != null ? c.FirstName : null,
                CustomerLastName  = c != null ? c.LastName  : null,
            }).ToListAsync();

        // Compute prevValue per (asset, ordered ascending) by walking through.
        // Single pass per asset key.
        var byAsset = rows
            .GroupBy(r => r.AssetId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CreatedAt).ToList());

        var prevByValuationId = new Dictionary<Guid, decimal?>();
        foreach (var (_, list) in byAsset)
        {
            decimal? prev = null;
            foreach (var v in list)
            {
                prevByValuationId[v.Id] = prev;
                prev = v.ValuationAmount;
            }
        }

        var items = rows.Select(r => new
        {
            id          = ShortId("VAL", r.Id),
            valuationUuid = r.Id,
            date        = r.CreatedAt.ToString("yyyy-MM-dd"),
            asset       = ShortId("AST", r.AssetId),
            assetUuid   = r.AssetId,
            customer    = (string.IsNullOrWhiteSpace(r.CustomerFirstName) && string.IsNullOrWhiteSpace(r.CustomerLastName))
                            ? "Unknown"
                            : $"{r.CustomerFirstName} {r.CustomerLastName}".Trim(),
            description = r.AssetDescription,
            valuer      = r.ValuerName,
            licenseNo   = r.ValuerLicense,
            prevValue   = prevByValuationId.TryGetValue(r.Id, out var p) ? p : null,
            newValue    = r.ValuationAmount,
            currency    = r.Currency,
            notes       = r.Notes ?? "",
        }).ToList();

        return Ok(items);
    }

    // -------------------------------------------------------------------------
    // POST /api/admin/asset-valuations
    // Records a new valuation for an existing asset. Mirrors the persistence
    // logic of AssetGrpcService.SubmitValuation: insert AssetValuation row,
    // bump asset.LastValuationAmount/LastValuationDate, and promote
    // PendingVerification → Active so the asset starts showing up as Active
    // once it has at least one valuation on file.
    // -------------------------------------------------------------------------
    public sealed record SubmitValuationDto(
        Guid    AssetId,
        decimal Amount,
        string  Currency,
        string  ValuerName,
        string? LicenseNo,
        string? Notes);

    [HttpPost("asset-valuations")]
    public async Task<IActionResult> PostAssetValuation([FromBody] SubmitValuationDto body)
    {
        if (body is null || body.AssetId == Guid.Empty)
            return BadRequest(new { error = "assetId is required." });
        if (body.Amount <= 0)
            return BadRequest(new { error = "amount must be greater than zero." });
        if (string.IsNullOrWhiteSpace(body.ValuerName))
            return BadRequest(new { error = "valuerName is required." });

        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == body.AssetId && !a.IsDeleted);
        if (asset is null) return NotFound(new { error = "Asset not found." });

        var currency = string.IsNullOrWhiteSpace(body.Currency) ? "USD" : body.Currency.ToUpperInvariant();
        var now = DateTime.UtcNow;

        var valuation = new AssetValuation
        {
            AssetId         = asset.Id,
            ValuationAmount = body.Amount,
            Currency        = currency,
            ValuerName      = body.ValuerName.Trim(),
            ValuerLicense   = body.LicenseNo?.Trim() ?? string.Empty,
            Notes           = body.Notes?.Trim() ?? string.Empty,
            ReportImagePath = null,
            TenantId        = asset.TenantId,
            CreatedAt       = now,
            UpdatedAt       = now,
        };

        asset.LastValuationAmount = body.Amount;
        asset.LastValuationDate   = now;
        asset.UpdatedAt           = now;
        if (asset.Status == AssetStatus.PendingVerification)
            asset.Status = AssetStatus.Active;

        _db.AssetValuations.Add(valuation);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            id            = ShortId("VAL", valuation.Id),
            valuationUuid = valuation.Id,
            assetUuid     = asset.Id,
            asset         = ShortId("AST", asset.Id),
            date          = now.ToString("yyyy-MM-dd"),
            valuer        = valuation.ValuerName,
            licenseNo     = valuation.ValuerLicense,
            newValue      = valuation.ValuationAmount,
            currency      = valuation.Currency,
            notes         = valuation.Notes,
        });
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/audit-logs?page=0&pageSize=60&adminUserId=&action=
    // -------------------------------------------------------------------------
    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int    page        = 0,
        [FromQuery] int    pageSize    = 60,
        [FromQuery] string adminUserId = "",
        [FromQuery] string action      = "")
    {
        var query = _db.AuditLogs.AsQueryable();

        if (Guid.TryParse(adminUserId, out var adminGuid))
            query = query.Where(a => a.AdminUserId == adminGuid);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        var total = await query.CountAsync();

        var items = await query
            .GroupJoin(_db.AdminUsers, a => a.AdminUserId, u => u.Id, (a, users) => new { a, user = users.FirstOrDefault() })
            .OrderByDescending(x => x.a.CreatedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                id          = x.a.Id,
                adminUser   = x.user != null ? x.user.Username : ShortId("USR", x.a.AdminUserId),
                action      = x.a.Action,
                entityType  = x.a.EntityType,
                target      = x.a.EntityId.ToString().Substring(0, 8),
                details     = x.a.Details,
                ipAddress   = x.a.IpAddress,
                timestamp   = x.a.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            })
            .ToListAsync();

        return Ok(new { items, total });
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/system-configs?tenantId=
    // -------------------------------------------------------------------------
    [HttpGet("system-configs")]
    public async Task<IActionResult> GetSystemConfigs([FromQuery] string tenantId = "")
    {
        var query = _db.SystemConfigs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(tenantId))
            query = query.Where(c => c.TenantId == tenantId);

        var items = await query
            .OrderBy(c => c.Key)
            .Select(c => new
            {
                id        = c.Id,
                key       = c.Key,
                valueJson = c.ValueJson,
                tenantId  = c.TenantId,
                updatedBy = c.UpdatedBy,
                updatedAt = c.UpdatedAt.HasValue
                    ? c.UpdatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
                    : null,
            })
            .ToListAsync();

        return Ok(items);
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/bill-providers?category=&status=
    // -------------------------------------------------------------------------
    [HttpGet("bill-providers")]
    public async Task<IActionResult> GetBillProviders(
        [FromQuery] string category = "",
        [FromQuery] string status   = "")
    {
        var query = _db.BillProviders.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);
        else
            query = query.Where(p => p.DeletedAt == null);

        var items = await query
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .Select(p => new
            {
                id                    = p.Id,
                name                  = p.Name,
                code                  = p.Code,
                category              = p.Category,
                requiresMeterNumber   = p.RequiresMeterNumber,
                requiresAccountNumber = p.RequiresAccountNumber,
                minAmount             = p.MinAmount,
                maxAmount             = p.MaxAmount,
                currency              = p.Currency,
                status                = MapStatus(p.Status),
                countryCode           = p.CountryCode,
            })
            .ToListAsync();

        return Ok(items);
    }

    // -------------------------------------------------------------------------
    // GET /api/admin/customers/{shortId}/activity
    // Aggregates timeline of ALL activity on the account: KYC reviews,
    // verifications, transactions, loans, disputes, fraud alerts, audit logs.
    // -------------------------------------------------------------------------
    [HttpGet("customers/{shortId}/activity")]
    public async Task<IActionResult> GetCustomerActivity(string shortId)
    {
        // Resolve short id (e.g. ACC-000005) → all matching account GUIDs (one per currency)
        var allAccounts = await _db.Accounts
            .Select(a => new { a.Id, a.PhoneNumber })
            .ToListAsync();

        var primary = allAccounts.FirstOrDefault(a => ShortId("ACC", a.Id) == shortId);
        if (primary == null) return NotFound();

        // Group by phone so we capture both ZWG + USD accounts as one customer
        var phone = primary.PhoneNumber;
        var accountIds = allAccounts.Where(a => a.PhoneNumber == phone).Select(a => a.Id).ToHashSet();

        var events = new List<object>();

        // 1. Account creation
        var accounts = await _db.Accounts.Where(a => accountIds.Contains(a.Id)).ToListAsync();
        foreach (var a in accounts)
        {
            events.Add(new
            {
                timestamp = a.CreatedAt,
                category  = "Account",
                action    = "Account Created",
                detail    = $"{a.Currency} account opened",
                actor     = "system",
            });
            if (a.LastLoginAt.HasValue)
            {
                events.Add(new
                {
                    timestamp = a.LastLoginAt.Value,
                    category  = "Auth",
                    action    = "Last Login",
                    detail    = $"{a.Currency} account",
                    actor     = "user",
                });
            }
        }

        // 2. KYC documents uploaded
        var kycDocs = await _db.KycDocuments.Where(k => accountIds.Contains(k.AccountId)).ToListAsync();
        foreach (var k in kycDocs)
        {
            events.Add(new
            {
                timestamp = k.CreatedAt,
                category  = "KYC",
                action    = "Document Uploaded",
                detail    = $"{k.DocumentType} ({k.Status})",
                actor     = "user",
            });
            if (k.VerifiedAt.HasValue)
            {
                events.Add(new
                {
                    timestamp = k.VerifiedAt.Value,
                    category  = "KYC",
                    action    = "Document Verified",
                    detail    = $"{k.DocumentType} → {k.Status}",
                    actor     = "system",
                });
            }
        }

        // 3. KYC verifications (AI + manual reviews)
        var kycVers = await _db.KycVerifications.Where(v => accountIds.Contains(v.AccountId)).ToListAsync();
        foreach (var v in kycVers)
        {
            events.Add(new
            {
                timestamp = v.CreatedAt,
                category  = "KYC",
                action    = "AI Verification",
                detail    = $"Face match {v.FaceMatchScore:P0}, decision: {v.OverallDecision}",
                actor     = "ai",
            });
            if (v.ReviewedAt.HasValue)
            {
                events.Add(new
                {
                    timestamp = v.ReviewedAt.Value,
                    category  = "KYC",
                    action    = "Manual Review",
                    detail    = $"{v.OverallDecision}{(string.IsNullOrEmpty(v.RejectionReason) ? "" : $" — {v.RejectionReason}")}",
                    actor     = v.ReviewedBy ?? "admin",
                });
            }
        }

        // 4. Transactions
        var txns = await _db.Transactions
            .Where(t => accountIds.Contains(t.AccountId))
            .OrderByDescending(t => t.CreatedAt)
            .Take(200)
            .ToListAsync();
        foreach (var t in txns)
        {
            events.Add(new
            {
                timestamp = t.CreatedAt,
                category  = "Transaction",
                action    = t.Type ?? "Transaction",
                detail    = $"{t.Currency} {t.Amount:N2} ({t.Status})",
                actor     = "user",
            });
        }

        // 5. Loans
        var loans = await _db.Loans.Where(l => accountIds.Contains(l.AccountId)).ToListAsync();
        foreach (var l in loans)
        {
            events.Add(new
            {
                timestamp = l.CreatedAt,
                category  = "Loan",
                action    = "Loan Applied",
                detail    = $"Principal {l.Principal:N2}, status: {l.Status}",
                actor     = "user",
            });
        }

        // 6. Disputes
        var disputes = await _db.Disputes.Where(d => accountIds.Contains(d.AccountId)).ToListAsync();
        foreach (var d in disputes)
        {
            events.Add(new
            {
                timestamp = d.CreatedAt,
                category  = "Dispute",
                action    = "Dispute Filed",
                detail    = $"{d.Type} — {d.Status}",
                actor     = "user",
            });
            if (d.ResolvedAt.HasValue)
            {
                events.Add(new
                {
                    timestamp = d.ResolvedAt.Value,
                    category  = "Dispute",
                    action    = "Dispute Resolved",
                    detail    = d.Resolution ?? d.Status.ToString(),
                    actor     = "admin",
                });
            }
        }

        // 7. Fraud alerts
        var fraud = await _db.FraudAlerts.Where(f => accountIds.Contains(f.AccountId)).ToListAsync();
        foreach (var f in fraud)
        {
            events.Add(new
            {
                timestamp = f.CreatedAt,
                category  = "Fraud",
                action    = "Fraud Alert",
                detail    = $"{f.AlertType} ({f.Severity}) — {f.Status}",
                actor     = "system",
            });
        }

        // 8. Audit logs targeting any of these account IDs (EntityId is string)
        var accountIdStrings = accountIds.Select(g => g.ToString()).ToHashSet();
        var auditRows = await _db.AuditLogs
            .Where(a => accountIdStrings.Contains(a.EntityId))
            .GroupJoin(_db.AdminUsers, a => a.AdminUserId, u => u.Id, (a, users) => new { a, user = users.FirstOrDefault() })
            .ToListAsync();
        foreach (var x in auditRows)
        {
            events.Add(new
            {
                timestamp = x.a.CreatedAt,
                category  = "Audit",
                action    = x.a.Action,
                detail    = x.a.Details ?? x.a.EntityType,
                actor     = x.user?.Username ?? "admin",
            });
        }

        // Order by timestamp DESC and project to a uniform shape
        var ordered = events
            .OrderByDescending(e => (DateTime)e.GetType().GetProperty("timestamp")!.GetValue(e)!)
            .Select(e => new
            {
                timestamp = ((DateTime)e.GetType().GetProperty("timestamp")!.GetValue(e)!).ToString("yyyy-MM-dd HH:mm:ss"),
                category  = e.GetType().GetProperty("category")!.GetValue(e),
                action    = e.GetType().GetProperty("action")!.GetValue(e),
                detail    = e.GetType().GetProperty("detail")!.GetValue(e),
                actor     = e.GetType().GetProperty("actor")!.GetValue(e),
            })
            .ToList();

        return Ok(ordered);
    }

    // -------------------------------------------------------------------------
    // POST /api/admin/customers/{shortId}/actions
    // Records an admin action against a customer (Activate/Suspend/Freeze/etc.)
    // by writing an audit log entry. Optionally updates account status.
    // -------------------------------------------------------------------------
    [HttpPost("customers/{shortId}/actions")]
    public async Task<IActionResult> AddCustomerAction(string shortId, [FromBody] CustomerActionEntry body)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.Action))
            return BadRequest("action is required");

        var allAccounts = await _db.Accounts.Select(a => new { a.Id, a.PhoneNumber }).ToListAsync();
        var primary = allAccounts.FirstOrDefault(a => ShortId("ACC", a.Id) == shortId);
        if (primary == null) return NotFound();

        // Update status across all accounts for this phone (ZWG + USD pair)
        var phone = primary.PhoneNumber;
        var matchingIds = allAccounts.Where(a => a.PhoneNumber == phone).Select(a => a.Id).ToList();

        var newStatus = body.Action.ToLowerInvariant() switch
        {
            "activate" => "active",
            "suspend"  => "suspended",
            "freeze"   => "frozen",
            "unfreeze" => "active",
            "close"    => "closed",
            _          => null,
        };

        if (newStatus != null)
        {
            await _db.Accounts
                .Where(a => matchingIds.Contains(a.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, newStatus));
        }

        // Write one audit log entry per account so the activity feed picks it up
        foreach (var aid in matchingIds)
        {
            _db.AuditLogs.Add(new GoldBank.Core.Modules.Admin.Domain.Entities.AuditLog
            {
                AdminUserId = Guid.Empty,
                Action      = body.Action,
                EntityType  = "Account",
                EntityId    = aid.ToString(),
                Details     = body.Reason ?? "",
                IpAddress   = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                CreatedAt   = DateTime.UtcNow,
            });
        }
        await _db.SaveChangesAsync();

        return Ok(new { ok = true, action = body.Action, status = newStatus });
    }

    public sealed class CustomerActionEntry
    {
        [System.Text.Json.Serialization.JsonPropertyName("action")]
        public string Action { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("reason")]
        public string Reason { get; set; } = "";
    }

    private static string ShortId(string prefix, Guid id)
    {
        // Extract a meaningful number from sequential UUIDs like 00000005-0000-0040-8000-000000000003
        var hex = id.ToString("N"); // 32 hex chars
        var lastPart = Convert.ToInt64(hex.Substring(24, 8), 16); // last 8 hex = unique part
        return $"{prefix}-{lastPart:D6}";
    }

    // Asset enum → display string helpers, used by GET /api/admin/assets so the
    // bank-client filter dropdowns ("Gold Coin" etc.) keep matching against the
    // values the API returns.
    private static string AssetTypeDisplay(AssetType t) => t switch
    {
        AssetType.GoldCoin       => "Gold Coin",
        AssetType.GoldBar        => "Gold Bar",
        AssetType.Silver         => "Silver Bar",
        AssetType.Platinum       => "Platinum Bar",
        AssetType.PreciousStone  => "Precious Stone",
        _                         => "Other",
    };

    private static string AssetStatusDisplay(AssetStatus s) => s switch
    {
        AssetStatus.Active                 => "Active",
        AssetStatus.PendingVerification    => "Pending Review",
        AssetStatus.PendingRelease         => "Pending Review",
        AssetStatus.Suspended              => "Inactive",
        AssetStatus.Released               => "Inactive",
        _                                   => s.ToString(),
    };

    private static string VerificationStatusDisplay(VerificationStatus v) => v switch
    {
        VerificationStatus.Verified => "Verified",
        VerificationStatus.Pending  => "Not Verified",
        VerificationStatus.Failed   => "Not Verified",
        VerificationStatus.Expired  => "Partial",
        _                            => v.ToString(),
    };

    private static string MapStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "active" => "Active",
        "suspended" => "Suspended",
        "frozen" => "Frozen",
        "closed" => "Closed",
        "pending_kyc" => "Pending KYC",
        "pending" => "Pending",
        "completed" => "Completed",
        "failed" => "Failed",
        "reversed" => "Reversed",
        "processing" => "Processing",
        "approved" => "Approved",
        "rejected" => "Rejected",
        "open" => "Open",
        "investigating" => "Investigating",
        "resolved" => "Resolved",
        "defaulted" => "Defaulted",
        "paid_off" => "Paid Off",
        "disbursed" => "Disbursed",
        "new" => "New",
        "reviewed" => "Reviewed",
        "escalated" => "Escalated",
        "dismissed" => "Dismissed",
        null or "" => "Unknown",
        _ => char.ToUpper(status[0]) + status[1..],
    };
}
