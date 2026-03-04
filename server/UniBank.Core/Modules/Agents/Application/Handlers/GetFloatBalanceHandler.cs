using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Agents.Application.Handlers;

/// <summary>
/// Result for a float balance query.
/// </summary>
public sealed record FloatBalanceResult(
    Guid AgentId,
    decimal FloatBalance,
    decimal FloatLimit,
    decimal AvailableFloat,
    string Currency);

/// <summary>
/// Handles float balance queries for an agent merchant (STORY-035).
/// Returns the current float balance, limit, and available float.
/// </summary>
public sealed class GetFloatBalanceHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<GetFloatBalanceHandler> _logger;

    public GetFloatBalanceHandler(
        UniBankDbContext dbContext,
        ILogger<GetFloatBalanceHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<FloatBalanceResult>> HandleAsync(
        Guid agentMerchantId, CancellationToken cancellationToken = default)
    {
        var agentFloat = await _dbContext.AgentFloats
            .FirstOrDefaultAsync(
                f => f.MerchantId == agentMerchantId && f.DeletedAt == null,
                cancellationToken);

        if (agentFloat is null)
            return Result.Failure<FloatBalanceResult>(
                new Error("Agent.NoFloat", "Agent float account not found."));

        var availableFloat = agentFloat.FloatLimit - agentFloat.FloatBalance;
        if (availableFloat < 0)
            availableFloat = 0;

        _logger.LogDebug(
            "Float balance queried for agent {AgentId}: balance={Balance}, limit={Limit}, available={Available}",
            agentMerchantId, agentFloat.FloatBalance, agentFloat.FloatLimit, availableFloat);

        return Result.Success(new FloatBalanceResult(
            AgentId: agentMerchantId,
            FloatBalance: agentFloat.FloatBalance,
            FloatLimit: agentFloat.FloatLimit,
            AvailableFloat: availableFloat,
            Currency: agentFloat.Currency));
    }
}
