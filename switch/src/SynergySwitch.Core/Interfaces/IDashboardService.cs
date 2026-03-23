using SynergySwitch.Core.Models;

namespace SynergySwitch.Core.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(DateTime? from = null, DateTime? to = null);
    Task<PagedResult<TransactionSummary>> GetTransactionsAsync(TransactionFilter filter);
    Task<TransactionSummary?> GetTransactionByIdAsync(int id);
    Task<IReadOnlyList<TerminalStatus>> GetTerminalStatusesAsync();
    Task<IReadOnlyList<HourlyThroughput>> GetHourlyThroughputAsync(DateTime from, DateTime to);
}
