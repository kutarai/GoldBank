using Microsoft.Extensions.Logging;
using GoldBank.Core.Modules.Accounts.Application.Commands;
using GoldBank.SharedKernel.Caching;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Accounts.Application.Handlers;

/// <summary>
/// Handles logout by revoking refresh tokens (STORY-018).
/// </summary>
public sealed class LogoutHandler
{
    private readonly ICacheStore _cache;
    private readonly ILogger<LogoutHandler> _logger;

    public LogoutHandler(ICacheStore cache, ILogger<LogoutHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(LogoutCommand command, CancellationToken cancellationToken = default)
    {
        var pattern = $"refresh_token:{command.AccountId}:*";
        var count = await _cache.DeleteByPatternAsync(pattern, cancellationToken);

        _logger.LogInformation(
            command.AllDevices
                ? "All sessions revoked for account {AccountId}, tokens: {Count}"
                : "Session revoked for account {AccountId}, tokens: {Count}",
            command.AccountId, count);

        return Result.Success();
    }
}
