namespace GoldBank.SharedKernel.Domain;

public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
    public string? TenantId { get; init; }
    public string? CorrelationId { get; init; }
}
