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

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                id          = a.Id,
                name        = (a.FirstName + " " + a.LastName).Trim(),
                phone       = a.PhoneCountryCode + a.PhoneNumber,
                email       = a.Email,
                status      = a.Status,
                kycLevel    = a.KycLevel,
                balanceZwg  = a.Currency == "ZWG" ? a.Balance : (decimal?)null,
                balanceUsd  = a.Currency == "USD" ? a.Balance : (decimal?)null,
                currency    = a.Currency,
                balance     = a.Balance,
                nationalId  = a.NationalId,
                created     = a.CreatedAt.ToString("yyyy-MM-dd"),
                lastLogin   = a.LastLoginAt.HasValue
                    ? a.LastLoginAt.Value.ToString("yyyy-MM-dd HH:mm")
                    : null,
            })
            .ToListAsync();

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
                id           = t.Id,
                accountId    = t.AccountId,
                type         = t.Type,
                amount       = t.Amount,
                fee          = t.Fee,
                status       = t.Status,
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
                id            = d.Id,
                transactionId = d.TransactionId,
                accountId     = d.AccountId,
                type          = d.Type.ToString(),
                description   = d.Description,
                status        = d.Status.ToString(),
                resolution    = d.Resolution,
                refundAmount  = d.RefundAmount,
                refundCurrency= d.RefundCurrency,
                adminUserId   = d.AdminUserId,
                filed         = d.CreatedAt.ToString("yyyy-MM-dd"),
                resolvedAt    = d.ResolvedAt.HasValue
                    ? d.ResolvedAt.Value.ToString("yyyy-MM-dd HH:mm")
                    : null,
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
                id            = f.Id,
                accountId     = f.AccountId,
                transactionId = f.TransactionId,
                type          = f.AlertType,
                severity      = f.Severity,
                description   = f.Description,
                status        = f.Status,
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
            query = query.Where(k => k.Status == status);
        else
            // Default: show documents awaiting review
            query = query.Where(k => k.Status == "uploaded" || k.Status == "pending");

        var items = await query
            .OrderBy(k => k.CreatedAt)
            .Select(k => new
            {
                id            = k.Id,
                accountId     = k.AccountId,
                documentType  = k.DocumentType,
                fileName      = k.FileName,
                contentType   = k.ContentType,
                fileSizeBytes = k.FileSizeBytes,
                status        = k.Status,
                submittedDate = k.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                verifiedAt    = k.VerifiedAt.HasValue
                    ? k.VerifiedAt.Value.ToString("yyyy-MM-dd HH:mm")
                    : null,
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
            .OrderByDescending(l => l.CreatedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                id                 = l.Id,
                accountId          = l.AccountId,
                reference          = l.Reference,
                principal          = l.Principal,
                outstandingBalance = l.OutstandingBalance,
                interestRate       = l.InterestRate,
                tenureMonths       = l.TenureMonths,
                monthlyPayment     = l.MonthlyPayment,
                purpose            = l.Purpose,
                status             = l.Status,
                creditScore        = l.CreditScore,
                paymentsMade       = l.PaymentsMade,
                currency           = l.Currency,
                appliedDate        = l.CreatedAt.ToString("yyyy-MM-dd"),
                disbursedAt        = l.DisbursedAt.HasValue
                    ? l.DisbursedAt.Value.ToString("yyyy-MM-dd")
                    : null,
                completedAt        = l.CompletedAt.HasValue
                    ? l.CompletedAt.Value.ToString("yyyy-MM-dd")
                    : null,
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
                status             = m.Status,
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
            .OrderByDescending(a => a.CreatedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                id          = a.Id,
                adminUserId = a.AdminUserId,
                action      = a.Action,
                entityType  = a.EntityType,
                entityId    = a.EntityId,
                details     = a.Details,
                ipAddress   = a.IpAddress,
                timestamp   = a.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
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
                status                = p.Status,
                countryCode           = p.CountryCode,
            })
            .ToListAsync();

        return Ok(items);
    }
}
