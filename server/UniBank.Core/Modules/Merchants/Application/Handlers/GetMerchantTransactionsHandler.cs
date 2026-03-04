using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.Core.Modules.Merchants.Domain.Entities;
using UniBank.Core.Modules.Payments.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Merchants.Application.Handlers;

/// <summary>
/// Retrieves paginated transaction history for a merchant (STORY-053).
/// Queries payments where the merchant is the recipient, supports date range
/// filtering and pagination, and masks payer phone numbers for privacy.
/// </summary>
public sealed class GetMerchantTransactionsHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<GetMerchantTransactionsHandler> _logger;

    public GetMerchantTransactionsHandler(
        UniBankDbContext dbContext,
        ILogger<GetMerchantTransactionsHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<MerchantTransactionListResult>> HandleAsync(
        GetMerchantTransactionsQuery query, CancellationToken cancellationToken = default)
    {
        var merchant = await _dbContext.Set<Merchant>()
            .FirstOrDefaultAsync(m => m.Id == query.MerchantId, cancellationToken);

        if (merchant is null)
            return Result.Failure<MerchantTransactionListResult>(
                new Error("Merchant.NotFound", "Merchant not found."));

        var paymentsQuery = _dbContext.Set<Payment>()
            .Where(p => p.MerchantAccountId == query.MerchantId && p.DeletedAt == null);

        // Apply date range filter
        if (query.DateFrom.HasValue)
            paymentsQuery = paymentsQuery.Where(p => p.CreatedAt >= query.DateFrom.Value);

        if (query.DateTo.HasValue)
            paymentsQuery = paymentsQuery.Where(p => p.CreatedAt < query.DateTo.Value);

        // Apply type filter
        if (!string.IsNullOrEmpty(query.TypeFilter))
            paymentsQuery = paymentsQuery.Where(p => p.Type == query.TypeFilter);

        // Get total count for pagination
        var totalCount = await paymentsQuery.CountAsync(cancellationToken);

        // Apply ordering and pagination
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var totalPages = totalCount > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;

        var payments = await paymentsQuery
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Look up payer accounts for masked phone numbers
        var payerAccountIds = payments.Select(p => p.PayerAccountId).Distinct().ToList();
        var payerAccounts = await _dbContext.Set<Account>()
            .Where(a => payerAccountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.PhoneNumber })
            .ToDictionaryAsync(a => a.Id, a => a.PhoneNumber, cancellationToken);

        var items = payments.Select(p =>
        {
            var maskedPhone = payerAccounts.TryGetValue(p.PayerAccountId, out var phone)
                ? MaskPhoneNumber(phone)
                : "***";

            return new MerchantTransactionItem(
                TransactionId: p.Id,
                Amount: p.Amount,
                Fee: p.Fee,
                Currency: p.Currency,
                Type: p.Type,
                Status: p.Status,
                Reference: p.Reference,
                PayerPhone: maskedPhone,
                TerminalId: p.TerminalId,
                CreatedAt: p.CreatedAt,
                CompletedAt: p.CompletedAt);
        }).ToList();

        _logger.LogInformation(
            "Retrieved {Count} transactions for merchant {MerchantId}, page {Page}/{TotalPages}",
            items.Count, query.MerchantId, page, totalPages);

        return Result.Success(new MerchantTransactionListResult(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages));
    }

    /// <summary>
    /// Masks a phone number for privacy, showing only the last 4 digits.
    /// Example: "+27821234567" becomes "***4567"
    /// </summary>
    private static string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 4)
            return "***";

        return $"***{phoneNumber[^4..]}";
    }
}

public sealed record GetMerchantTransactionsQuery(
    Guid MerchantId,
    DateTime? DateFrom,
    DateTime? DateTo,
    int Page,
    int PageSize,
    string? TypeFilter);

public sealed record MerchantTransactionListResult(
    IReadOnlyList<MerchantTransactionItem> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record MerchantTransactionItem(
    Guid TransactionId,
    decimal Amount,
    decimal Fee,
    string Currency,
    string Type,
    string Status,
    string Reference,
    string PayerPhone,
    string? TerminalId,
    DateTime CreatedAt,
    DateTime? CompletedAt);
