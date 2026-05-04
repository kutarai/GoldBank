namespace GoldBank.SharedKernel.Events;

using GoldBank.SharedKernel.Domain;

public sealed record LowFloatAlert(
    Guid AgentId,
    string AgentCode,
    decimal CurrentBalance,
    decimal ThresholdAmount,
    string Currency) : DomainEvent;
