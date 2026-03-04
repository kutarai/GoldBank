namespace UniBank.SharedKernel.Events;

using UniBank.SharedKernel.Domain;

public sealed record FraudAlertRaised(
    Guid TransactionId,
    Guid AccountId,
    string AlertType,
    string Description,
    string Severity) : DomainEvent;
