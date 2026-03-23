using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Payments.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Payments.Application.Handlers;

/// <summary>
/// Handles card tokenization for NFC payments (STORY-022).
/// Generates a format-preserving token (same length as PAN, starts with token prefix),
/// stores the mapping, and returns the token reference for use in NFC transactions.
/// </summary>
public sealed class TokenizeCardHandler
{
    private const string TokenPrefix = "9999";
    private const int TokenValidityDays = 365;
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<TokenizeCardHandler> _logger;

    public TokenizeCardHandler(
        UniBankDbContext dbContext,
        ILogger<TokenizeCardHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<TokenizeCardResult>> HandleAsync(
        Commands.TokenizeCardCommand command, CancellationToken cancellationToken = default)
    {
        // Verify account exists and is active
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == command.AccountId && a.DeletedAt == null,
                cancellationToken);

        if (account is null)
            return Result.Failure<TokenizeCardResult>(
                new Error("Account.NotFound", "Account not found."));

        if (account.Status != "active")
            return Result.Failure<TokenizeCardResult>(
                new Error("Account.Inactive", "Account is not active. Current status: " + account.Status));

        // Use the account's virtual card PAN if client didn't provide one
        var cardPan = !string.IsNullOrWhiteSpace(command.CardPan) ? command.CardPan : account.CardPan;

        if (string.IsNullOrWhiteSpace(cardPan) || cardPan.Length < 13 || cardPan.Length > 19)
            return Result.Failure<TokenizeCardResult>(
                new Error("Token.InvalidPan", "No valid card PAN available for this account."));

        if (!cardPan.All(char.IsDigit))
            return Result.Failure<TokenizeCardResult>(
                new Error("Token.InvalidPan", "Card PAN must contain only digits."));

        // Check for existing active token for this account + device combination
        var existingToken = await _dbContext.Set<PaymentToken>()
            .FirstOrDefaultAsync(
                t => t.AccountId == command.AccountId
                     && t.DeviceId == command.DeviceId
                     && t.Status == "active"
                     && t.DeletedAt == null,
                cancellationToken);

        if (existingToken is not null)
        {
            // Revoke existing token before creating a new one
            existingToken.Status = "revoked";
            existingToken.UpdatedAt = DateTime.UtcNow;
        }

        // Generate format-preserving token: same length as PAN, starts with prefix
        var token = GenerateFormatPreservingToken(cardPan.Length);
        var tokenReference = GenerateTokenReference();
        var cardPanLast4 = cardPan[^4..];

        var paymentToken = new PaymentToken
        {
            AccountId = command.AccountId,
            Token = token,
            TokenReference = tokenReference,
            CardPanLast4 = cardPanLast4,
            DeviceId = command.DeviceId,
            Status = "active",
            ExpiresAt = DateTime.UtcNow.AddDays(TokenValidityDays),
            TenantId = command.TenantId
        };

        _dbContext.Set<PaymentToken>().Add(paymentToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Card tokenized for account {AccountId}, device {DeviceId}, last4: {Last4}, ref: {Ref}",
            command.AccountId, command.DeviceId, cardPanLast4, tokenReference);

        return Result.Success(new TokenizeCardResult(
            Token: token,
            TokenReference: tokenReference,
            CardPanLast4: cardPanLast4));
    }

    /// <summary>
    /// Generates a format-preserving token with the same length as the original PAN.
    /// Uses a fixed prefix (9999) followed by cryptographically random digits.
    /// </summary>
    private static string GenerateFormatPreservingToken(int length)
    {
        var remainingLength = length - TokenPrefix.Length;
        Span<byte> randomBytes = stackalloc byte[remainingLength];
        RandomNumberGenerator.Fill(randomBytes);

        var chars = new char[length];
        TokenPrefix.CopyTo(0, chars, 0, TokenPrefix.Length);

        for (var i = 0; i < remainingLength; i++)
        {
            chars[TokenPrefix.Length + i] = (char)('0' + (randomBytes[i] % 10));
        }

        return new string(chars);
    }

    /// <summary>
    /// Generates a unique token reference for tracking purposes.
    /// </summary>
    private static string GenerateTokenReference()
    {
        return $"TKN-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
    }
}

/// <summary>
/// Result of a successful card tokenization.
/// </summary>
public sealed record TokenizeCardResult(
    string Token,
    string TokenReference,
    string CardPanLast4);
