namespace UniBank.Core.Modules.Agents.Application.Commands;

/// <summary>
/// Command to perform a cash-in operation at a merchant agent (STORY-032).
/// The agent receives physical cash from the customer and credits their account.
/// </summary>
public sealed record CashInCommand(
    Guid AgentMerchantId,
    string CustomerPhone,
    decimal Amount,
    string Currency,
    string AgentPin,
    string TenantId);
