namespace UniBank.SharedKernel.Events;

using UniBank.SharedKernel.Domain;

public sealed record LowFloatAlert(
    Guid AgentId,
    string AgentCode,
    decimal CurrentBalance,
    decimal ThresholdAmount,
    string Currency) : DomainEvent;
