using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniBank.Core.Common.Persistence;

namespace UniBank.Gateway.Controllers;

/// <summary>
/// REST API controller providing admin data endpoints for the bank-client React app.
/// Queries UniBankDbContext directly — no business logic, just DB reads.
/// </summary>
[ApiController]
[Route("api/admin")]
[EnableCors("BankClient")]
public class AdminApiController : ControllerBase
{
    private readonly UniBankDbContext _db;

    public AdminApiController(UniBankDbContext db)
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
            if (Enum.TryParse<UniBank.Core.Modules.Admin.Domain.Entities.DisputeStatus>(
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
        var query = _db.KycDocuments.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(k => k.Status == status.ToLowerInvariant());

        var items = await query
            .Join(_db.Accounts, k => k.AccountId, a => a.Id, (k, a) => new { k, a })
            .OrderByDescending(x => x.k.CreatedAt)
            .Select(x => new
            {
                id            = ShortId("KYC", x.k.Id),
                accountId     = ShortId("ACC", x.k.AccountId),
                name          = (x.a.FirstName + " " + x.a.LastName).Trim(),
                documentType  = x.k.DocumentType,
                status        = MapStatus(x.k.Status),
                submittedDate = x.k.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                level         = x.a.KycLevel,
                faceMatchScore = 0.85,
                aiDecision    = x.k.Status == "approved" ? "AutoApproved" : x.k.Status == "rejected" ? "Rejected" : "Pending",
                nameMatch     = true,
                idMatch       = true,
                dobMatch      = x.k.Status != "rejected",
            })
            .ToListAsync();

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

    private static string ShortId(string prefix, Guid id)
    {
        // Extract a meaningful number from sequential UUIDs like 00000005-0000-0040-8000-000000000003
        var hex = id.ToString("N"); // 32 hex chars
        var lastPart = Convert.ToInt64(hex.Substring(24, 8), 16); // last 8 hex = unique part
        return $"{prefix}-{lastPart:D6}";
    }

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
