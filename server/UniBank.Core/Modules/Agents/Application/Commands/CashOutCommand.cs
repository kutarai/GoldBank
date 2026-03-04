namespace UniBank.Core.Modules.Agents.Application.Commands;

/// <summary>
/// Command to perform a cash-out operation at a merchant agent (STORY-033).
/// The customer withdraws cash from their account via the agent.
/// </summary>
public sealed record CashOutCommand(
    Guid AgentMerchantId,
    Guid CustomerAccountId,
    decimal Amount,
    string Currency,
    string CustomerPin,
    string AgentPin,
    string TenantId);
