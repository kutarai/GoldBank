namespace GoldBank.SharedKernel.Events;

using GoldBank.SharedKernel.Domain;

public sealed record FraudAlertRaised(
    Guid TransactionId,
    Guid AccountId,
    string AlertType,
    string Description,
    string Severity) : DomainEvent;
